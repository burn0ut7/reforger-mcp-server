// The single Ghidra adapter. Requests/results are files; stdout belongs to Ghidra.
// @category Reforger.MCP
import ghidra.app.util.headless.HeadlessScript;
import ghidra.app.decompiler.*;
import ghidra.program.model.address.*;
import ghidra.program.model.listing.*;
import ghidra.program.model.symbol.*;
import com.google.gson.*;
import java.nio.file.*;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.util.*;

public class ReforgerBridge extends HeadlessScript {
    private final Gson gson = new GsonBuilder().disableHtmlEscaping().serializeNulls().create();
    private JsonObject request;
    private int offset, limit;
    public void run() throws Exception {
        String[] args = getScriptArgs();
        JsonObject response = new JsonObject();
        try {
            request = JsonParser.parseString(Files.readString(Path.of(args[0]))).getAsJsonObject();
            if (!currentProgram.getExecutableSHA256().equalsIgnoreCase(text("sha256")))
                throw new IllegalArgumentException("Analysis binary identity does not match request");
            offset = request.get("offset").getAsInt();
            limit = request.get("limit").getAsInt();
            if (offset < 0 || offset > 1000000 || limit < 1 || limit > 100)
                throw new IllegalArgumentException("Invalid page bounds");
            response.addProperty("sha256", currentProgram.getExecutableSHA256());
            response.addProperty("imageBase", currentProgram.getImageBase().toString());
            response.addProperty("language", currentProgram.getLanguageID().toString());
            response.addProperty("compiler", currentProgram.getCompilerSpec().getCompilerSpecID().toString());
            switch (text("operation")) {
                case "prepare":
                    if (analysisTimeoutOccurred()) throw new IllegalStateException("Analysis timed out; project is incomplete");
                    response.addProperty("analysisCompleted", true);
                    response.addProperty("functionCount", currentProgram.getFunctionManager().getFunctionCount());
                    break;
                case "search": search(response); break;
                case "references": references(response); break;
                case "function": function(response); break;
                default: throw new IllegalArgumentException("Unknown operation");
            }
            response.addProperty("ok", true);
        } catch (Exception failure) {
            response.addProperty("ok", false);
            response.addProperty("error", failure.getClass().getSimpleName() + ": " + failure.getMessage());
        }
        Files.writeString(Path.of(args[1]), gson.toJson(response), StandardCharsets.UTF_8);
    }
    private String text(String key) { return request.get(key).getAsString(); }
    private boolean matches(String value, String[] terms) {
        String lower = value.toLowerCase(Locale.ROOT);
        for (String term : terms) if (!lower.contains(term)) return false;
        return true;
    }
    private JsonObject at(Address address) {
        JsonObject row = new JsonObject();
        row.addProperty("address", address.toString());
        if (address.isMemoryAddress()) row.addProperty("rva", Long.toHexString(address.subtract(currentProgram.getImageBase())));
        return row;
    }
    private String clip(String value) { return value.length() > 1500 ? value.substring(0,1500) : value; }
    private void value(JsonObject row, String key, String value) {
        if (value == null) { row.add(key, JsonNull.INSTANCE); return; }
        row.addProperty(key, clip(value));
        if (value.length() > 1500) row.addProperty(key + "Truncated", true);
    }
    private final class Page {
        JsonArray items = new JsonArray(); int total;
        void add(JsonObject item) { if (total >= offset && items.size() < limit) items.add(item); total++; }
        void finish(JsonObject out) {
            out.add("items", items); out.addProperty("total", total); out.addProperty("offset", offset);
            if (offset + items.size() < total) out.addProperty("nextOffset", offset + items.size());
            else out.add("nextOffset", JsonNull.INSTANCE);
        }
    }
    private void search(JsonObject out) throws Exception {
        String query = text("query").trim();
        if (query.isEmpty() || query.length() > 200) throw new IllegalArgumentException("Search needs 1-200 characters");
        String kind = text("kind");
        if (!List.of("strings", "symbols").contains(kind)) throw new IllegalArgumentException("Search kind must be strings or symbols");
        String[] terms = query.toLowerCase(Locale.ROOT).split("\\s+");
        Page page = new Page();
        if (kind.equals("strings")) {
            DataIterator data = currentProgram.getListing().getDefinedData(true);
            while (data.hasNext()) {
                monitor.checkCancelled(); Data d = data.next();
                if (!(d.getValue() instanceof String s) || !matches(s, terms)) continue;
                JsonObject row = at(d.getAddress()); value(row,"text",s);
                JsonArray refs = new JsonArray(); int count = 0;
                ReferenceIterator it = currentProgram.getReferenceManager().getReferencesTo(d.getAddress());
                while (it.hasNext()) {
                    monitor.checkCancelled(); Reference r = it.next(); count++;
                    if (refs.size() < 8) {
                        JsonObject edge = at(r.getFromAddress());
                        Function f = currentProgram.getFunctionManager().getFunctionContaining(r.getFromAddress());
                        if (f != null) { edge.addProperty("functionAddress",f.getEntryPoint().toString()); value(edge,"functionName", f.getName(true)); }
                        refs.add(edge);
                    }
                }
                row.add("references",refs); row.addProperty("referenceCount",count); row.addProperty("referencesTruncated",count > refs.size());
                page.add(row);
            }
            out.addProperty("coverage", "Defined strings and resolved references only; no hit does not prove absence.");
        } else {
            SymbolIterator symbols = currentProgram.getSymbolTable().getAllSymbols(true);
            while (symbols.hasNext()) {
                monitor.checkCancelled(); Symbol s = symbols.next();
                if (!matches(s.getName(true), terms)) continue;
                JsonObject row = at(s.getAddress()); value(row,"name",s.getName(true));
                row.addProperty("source", s.getSource().toString()); row.addProperty("symbolType",s.getSymbolType().toString());
                Function f = currentProgram.getFunctionManager().getFunctionContaining(s.getAddress());
                if (f != null) row.addProperty("functionAddress",f.getEntryPoint().toString());
                page.add(row);
            }
            out.addProperty("coverage", "Database symbols, including generated/imported names; not a complete original-source inventory.");
        }
        page.finish(out);
    }
    private void function(JsonObject out) throws Exception {
        String address = text("address");
        if (!address.matches("(?i)(0x)?[0-9a-f]{1,16}")) throw new IllegalArgumentException("Use a hexadecimal memory address returned by search");
        Address selected = toAddr(address.replaceFirst("(?i)^0x", ""));
        Function f = currentProgram.getFunctionManager().getFunctionContaining(selected);
        if (f == null) throw new IllegalArgumentException("No defined function contains this address");
        String section = text("section");
        if (!List.of("overview","code","callers","callees","instructions","context","types").contains(section))
            throw new IllegalArgumentException("Unknown function section");
        out.addProperty("entry", f.getEntryPoint().toString());
        out.addProperty("selectedAddress", selected.toString());
        out.addProperty("rva", Long.toHexString(f.getEntryPoint().subtract(currentProgram.getImageBase())));
        value(out,"name",f.getName(true)); value(out,"storedSignature",f.getSignature().toString());
        out.addProperty("signatureSource",f.getSignatureSource().toString());
        out.addProperty("bodyBytes",f.getBody().getNumAddresses());
        out.addProperty("section",section);
        out.addProperty("caution","Reconstructed evidence, not original C++ or a supported Enforce API. Types may be inferred; indirect targets may remain unknown.");
        Page page = new Page();
        if (section.equals("callers") || section.equals("callees")) {
            Set<Function> functions = section.equals("callers") ? f.getCallingFunctions(monitor) : f.getCalledFunctions(monitor);
            for (Function related : functions.stream().sorted(Comparator.comparing(Function::getEntryPoint)).toList()) {
                monitor.checkCancelled(); JsonObject row = at(related.getEntryPoint()); value(row,"name",related.getName(true));
                row.addProperty("external",related.isExternal()); page.add(row);
            }
            out.addProperty("coverage","Resolved unique functions only. Zero resolved callers does not prove a function is unused; computed calls are listed in context.");
        } else if (section.equals("instructions") || section.equals("context")) {
            InstructionIterator instructions = currentProgram.getListing().getInstructions(f.getBody(),true);
            while (instructions.hasNext()) {
                monitor.checkCancelled(); Instruction i = instructions.next();
                if (section.equals("instructions")) {
                    if (i.getAddress().compareTo(selected) < 0) continue;
                    JsonObject row = at(i.getAddress()); value(row,"instruction",i.toString()); page.add(row);
                }
                else {
                    if (i.getFlowType().isCall() && i.getFlowType().isComputed()) {
                        JsonObject row = at(i.getAddress()); row.addProperty("kind","computedCall"); value(row,"instruction",i.toString());
                        row.addProperty("targetResolution","not-established"); page.add(row);
                    }
                    for (Reference r : i.getReferencesFrom()) {
                        Data d = currentProgram.getListing().getDataAt(r.getToAddress());
                        Symbol s = currentProgram.getSymbolTable().getPrimarySymbol(r.getToAddress());
                        if (d != null && d.getValue() instanceof String str) {
                            JsonObject row = at(i.getAddress()); row.addProperty("kind","string"); row.addProperty("target",r.getToAddress().toString()); value(row,"text",str); page.add(row);
                        } else if (s != null && s.getSource() != SourceType.DEFAULT) {
                            JsonObject row = at(i.getAddress()); row.addProperty("kind","symbol"); row.addProperty("target",r.getToAddress().toString()); value(row,"name",s.getName(true)); row.addProperty("source",s.getSource().toString()); page.add(row);
                        }
                    }
                }
            }
        } else {
            DecompInterface decompiler = new DecompInterface();
            try {
                if (!decompiler.openProgram(currentProgram)) throw new IllegalStateException(decompiler.getLastMessage());
                DecompileResults result = decompiler.decompileFunction(f,30,monitor);
                if (!result.decompileCompleted() || result.getDecompiledFunction() == null)
                    throw new IllegalStateException("Decompilation incomplete: " + result.getErrorMessage());
                out.addProperty("decompileCompleted",true); value(out,"diagnostics",result.getErrorMessage());
                value(out,"inferredSignature",result.getDecompiledFunction().getSignature());
                String completeCode = result.getDecompiledFunction().getC();
                byte[] codeBytes = completeCode.getBytes(StandardCharsets.UTF_8);
                String codeHash = HexFormat.of().formatHex(MessageDigest.getInstance("SHA-256").digest(codeBytes));
                // Each query owns a fresh evidence directory. Retain one complete result:
                // independent decompilations can infer different signatures and line counts.
                Path codeFile = Path.of(getScriptArgs()[1]).resolveSibling("decompilation.c");
                Files.write(codeFile, codeBytes, StandardOpenOption.CREATE_NEW, StandardOpenOption.WRITE);
                out.addProperty("codeArtifact", "decompilation.c");
                out.addProperty("codeSha256", codeHash);
                out.addProperty("codeBytes", codeBytes.length);
                out.addProperty("paginationCaution", "Independent decompilations may differ. Combine code pages only when codeSha256 matches; the complete single-run text is retained in codeArtifact under evidenceDirectory.");
                String[] lines = completeCode.split("\\R",-1);
                JsonArray warnings = new JsonArray();
                for (String line : lines) if (line.contains("WARNING:")) warnings.add(clip(line));
                out.add("warnings",warnings);
                if (section.equals("code")) {
                    for (int n=0; n<lines.length; n++) { JsonObject row = new JsonObject(); row.addProperty("line",n+1); value(row,"text",lines[n]); page.add(row); }
                } else if (section.equals("types")) {
                    if (result.getHighFunction() != null) {
                        var symbols = result.getHighFunction().getLocalSymbolMap().getSymbols();
                        while (symbols.hasNext()) { var s = symbols.next(); JsonObject row = new JsonObject(); value(row,"name",s.getName()); value(row,"inferredType",s.getDataType().getPathName()); page.add(row); }
                    }
                } else {
                    out.addProperty("codeLines",lines.length);
                    out.addProperty("resolvedCallerCount",f.getCallingFunctions(monitor).size());
                    out.addProperty("resolvedCalleeCount",f.getCalledFunctions(monitor).size());
                    out.addProperty("nextStep","Read context for strings/symbols/computed calls, code for paged pseudocode, callers/callees for navigation, instructions for verification, types for inferred variables.");
                }
            } finally { decompiler.dispose(); }
        }
        page.finish(out);
    }
    private void references(JsonObject out) throws Exception {
        String address = text("address");
        if (!address.matches("(?i)(0x)?[0-9a-f]{1,16}")) throw new IllegalArgumentException("Use a hexadecimal memory address");
        Address selected = toAddr(address.replaceFirst("(?i)^0x", ""));
        out.addProperty("selectedAddress", selected.toString());
        JsonArray outgoing = new JsonArray();
        // A bounded database-memory preview makes referenced constants inspectable
        // without another binary reader or an inferred decompiler value.
        JsonObject memory = new JsonObject();
        var block = currentProgram.getMemory().getBlock(selected);
        if (block == null || !block.isInitialized()) {
            memory.addProperty("status", block == null ? "unmapped" : "uninitialized");
        } else {
            int count = (int)Math.min(16L, block.getEnd().subtract(selected) + 1);
            byte[] bytes = new byte[count];
            int read = currentProgram.getMemory().getBytes(selected, bytes);
            if (read != count) throw new IllegalStateException("Incomplete database-memory preview");
            memory.addProperty("status", "available");
            memory.addProperty("hex", HexFormat.of().formatHex(bytes));
            memory.addProperty("byteCount", count);
            memory.addProperty("block", block.getName());
            memory.addProperty("bigEndian", currentProgram.getLanguage().isBigEndian());
        }
        memory.addProperty("coverage", "Up to 16 bytes at selectedAddress in the analyzed Ghidra database, bounded by its memory block. Not live process memory or a complete data object.");
        out.add("memoryPreview", memory);
        int outgoingCount = 0;
        for (Reference ref : currentProgram.getReferenceManager().getReferencesFrom(selected)) {
            monitor.checkCancelled();
            outgoingCount++;
            if (outgoing.size() >= 100) continue;
            JsonObject row = at(ref.getToAddress());
            row.addProperty("referenceType", ref.getReferenceType().toString());
            Symbol symbol = currentProgram.getSymbolTable().getPrimarySymbol(ref.getToAddress());
            if (symbol != null) { value(row,"name",symbol.getName(true)); row.addProperty("source",symbol.getSource().toString()); }
            Function target = currentProgram.getFunctionManager().getFunctionAt(ref.getToAddress());
            if (target != null) { row.addProperty("functionAddress",target.getEntryPoint().toString()); row.addProperty("external",target.isExternal()); }
            outgoing.add(row);
        }
        out.add("outgoingReferences", outgoing);
        out.addProperty("outgoingReferenceCount", outgoingCount);
        out.addProperty("outgoingReferencesTruncated", outgoingCount > outgoing.size());
        Page page = new Page();
        ReferenceIterator references = currentProgram.getReferenceManager().getReferencesTo(selected);
        while (references.hasNext()) {
            monitor.checkCancelled(); Reference ref = references.next(); JsonObject row = at(ref.getFromAddress());
            row.addProperty("referenceType", ref.getReferenceType().toString());
            Function f = currentProgram.getFunctionManager().getFunctionContaining(ref.getFromAddress());
            if (f != null) { row.addProperty("functionAddress",f.getEntryPoint().toString()); value(row,"functionName",f.getName(true)); }
            page.add(row);
        }
        out.addProperty("coverage","Paged resolved inbound references and up to 100 resolved outgoing references at the exact selected address. Outgoing references can identify defined pointer/vtable targets; absence does not prove absence of a runtime target. Indirect/dynamic references may be unknown.");
        page.finish(out);
    }
}
