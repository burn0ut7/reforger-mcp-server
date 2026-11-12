// Compile the production dispatcher in this test translation unit so its
// private native-owner boundary can be supplied by a memory-safe fixture.
// This does not claim to validate Workbench addresses or reconstruction.
#include "NativeArrayCommands.cpp"
#include <array>
#include <cstdlib>
#include <cstdio>

namespace {
int allocations = 0, delegatedUndo = 0, delegatedRedo = 0, destroyed = 0;
quintptr lastDestroyFlags = 0;
bool failNextCopy = false;
int protectionCalls = 0, exchangeCalls = 0, failProtection = 0, failExchange = 0;
DWORD currentProtection = PAGE_READONLY;
BOOL WINAPI protectFixture(LPVOID, SIZE_T, DWORD requested, PDWORD previous)
{
    if (++protectionCalls == failProtection) return FALSE;
    *previous = currentProtection;
    currentProtection = requested;
    return TRUE;
}
void *exchangeFixture(void **slot, void *replacement, void *expected)
{
    if (++exchangeCalls == failExchange) return reinterpret_cast<void *>(99);
    const auto old = *slot;
    if (old == expected) *slot = replacement;
    return old;
}
void require(bool condition, const char *name)
{
    if (!condition) { std::fprintf(stderr, "FAIL %s\n", name); std::exit(1); }
    std::printf("PASS %s\n", name);
}
void freeString(void *allocation, int)
{
    --allocations;
    std::free(allocation);
}
void releaseString(char *text)
{
    if (text && InterlockedDecrement(reinterpret_cast<volatile LONG *>(text - 4)) == 0)
        freeString(text - 12, 0);
}
void assignString(char **output, const char *bytes, unsigned length)
{
    auto *allocation = static_cast<char *>(std::malloc(size_t(length) + 13));
    require(allocation != nullptr, "Fixture allocation");
    const unsigned header[] = {length + 1, length, 1};
    std::memcpy(allocation, header, 12);
    std::memcpy(allocation + 12, bytes, length);
    allocation[12 + length] = 0;
    releaseString(*output);
    *output = allocation + 12;
    ++allocations;
}
void clearArray(rst::ArrayView &array)
{
    for (unsigned i = 0; i < array.count; ++i) releaseString(array.items[i]);
    std::free(array.items);
    array = {};
}
void *copyArray(void *destination, const rst::ArrayView *source)
{
    auto &array = *static_cast<rst::ArrayView *>(destination);
    if (failNextCopy) {
        failNextCopy = false;
        clearArray(array);
        return destination;
    }
    std::vector<char *> retained;
    if (source->count) retained.assign(source->items, source->items + source->count);
    for (auto *text : retained) if (text) InterlockedIncrement(reinterpret_cast<volatile LONG *>(text - 4));
    clearArray(array);
    if (!retained.empty()) {
        array.items = static_cast<char **>(std::malloc(retained.size() * sizeof(char *)));
        require(array.items != nullptr, "Fixture array allocation");
        std::memcpy(array.items, retained.data(), retained.size() * sizeof(char *));
    }
    array.capacity = array.count = unsigned(retained.size());
    return destination;
}
struct Descriptor {
    unsigned type = 0x8a;
    unsigned short index = 0, unused = 0;
    const char *key = "references";
    char remainder[72]{};
};
static_assert(sizeof(Descriptor) == 0x58);
struct Schema {
    char prefix[0x20]{};
    const char *name = "Fixture";
    Descriptor *descriptors;
    unsigned capacity = 1, count = 1;
};
struct Source {
    quintptr *vtable;
    quint64 id = 17, references = 1;
    Schema *schema;
    rst::ArrayView values{};
    bool local = true;
};
const void *getValues(void *source, unsigned) { return &static_cast<Source *>(source)->values; }
const void *getLocal(void *source, unsigned)
{
    auto *value = static_cast<Source *>(source);
    return value->local ? &value->values : nullptr;
}
void *getWritable(void *source, unsigned)
{
    auto *value = static_cast<Source *>(source);
    value->local = true;
    return &value->values;
}
void clearLocal(void *source, unsigned)
{
    auto *value = static_cast<Source *>(source);
    value->local = false;
    clearArray(value->values);
}
struct FakeAction {
    void *vtable = nullptr;
    Source *source;
    quint64 padding = 0;
    unsigned short variable = 0;
    char reserved[6]{};
    char *text = nullptr, *oldText = nullptr;
};
static_assert(offsetof(FakeAction, text) == 0x20);
struct FakeCommand {
    char prefix[0x28]{};
    FakeAction action;
};
void *destroy(void *command, quintptr flags)
{
    ++destroyed;
    lastDestroyFlags = flags;
    auto &action = static_cast<FakeCommand *>(command)->action;
    releaseString(action.text);
    releaseString(action.oldText);
    action.text = action.oldText = nullptr;
    return command;
}
std::unique_ptr<FakeCommand> command(Source &source, const QByteArray &wire)
{
    auto value = std::make_unique<FakeCommand>();
    value->action.source = &source;
    assignString(&value->action.text, wire.constData(), unsigned(wire.size()));
    return value;
}
bool equals(Source &source, const QStringList &expected)
{
    QStringList observed;
    bool local = false;
    return rst::readArray(&source, 0, observed, local) && local && observed == expected;
}
std::shared_ptr<rst::Plan> plan(Source &source, const QStringList &before, const QStringList &after, const QString &receipt)
{
    auto value = std::make_shared<rst::Plan>();
    value->identity = rst::sourceIdentity(&source);
    value->className = "Fixture";
    value->key = "references";
    value->before = before;
    value->after = after;
    require(rst::registerPlan(receipt, value).isEmpty(), "Fresh plan registered");
    return value;
}
}

int main()
{
    // Align the fixture just as the exact-profile slots share one native page.
    alignas(4096) std::array<void *, 3> fixtureSlots{};
    for (int scenario = 0; scenario < 4; ++scenario) {
        fixtureSlots = {reinterpret_cast<void *>(1), reinterpret_cast<void *>(2), reinterpret_cast<void *>(3)};
        const auto initial = fixtureSlots;
        std::array<rst::SlotPatch, 3> patches;
        for (size_t i = 0; i < fixtureSlots.size(); ++i)
            patches[i] = {&fixtureSlots[i], fixtureSlots[i], reinterpret_cast<void *>(i + 11)};
        currentProtection = PAGE_READONLY;
        protectionCalls = exchangeCalls = 0;
        failProtection = scenario == 1 ? 1 : scenario == 2 ? 2 : 0;
        failExchange = scenario == 3 ? 2 : 0;
        const bool patched = rst::patchSlots(patches, &protectFixture, &exchangeFixture);
        if (scenario == 0)
            require(patched && fixtureSlots[2] == reinterpret_cast<void *>(13), "Hook installation changes all intended slots");
        else
            require(!patched && fixtureSlots == initial, "Failed protection or partial installation restores original owners");
        require(currentProtection == PAGE_READONLY, "Hook path restores original page protection");
    }
    auto &state = rst::registry();
    state.thread = GetCurrentThreadId();
    state.owners = {[](void *) { ++delegatedUndo; }, [](void *) { ++delegatedRedo; },
        &destroy, &assignString, &copyArray, &freeString};
    std::array<quintptr, 0x138 / sizeof(quintptr)> table{};
    table[0xf8 / 8] = reinterpret_cast<quintptr>(&clearLocal);
    table[0x120 / 8] = reinterpret_cast<quintptr>(&getWritable);
    table[0x128 / 8] = reinterpret_cast<quintptr>(&getValues);
    table[0x130 / 8] = reinterpret_cast<quintptr>(&getLocal);
    Descriptor descriptor;
    Schema schema{{}, "Fixture", &descriptor};
    Source source{table.data(), 17, 1, &schema};
    const QStringList before{"comma,value", "old", " leading and trailing ", "\"quotes\"\\slash",
        QString::fromUtf8(QByteArray::fromHex("efbbbfe697a5f09f9880")), ""};
    auto after = before;
    after[1] = "NamedEntity";
    require(rst::writeArray(&source, 0, before, true) && equals(source, before), "Fixture retains complete literal array");
    auto payload = plan(source, before, after, "transaction");
    std::weak_ptr<rst::Plan> lifetime = payload;
    auto edit = command(source, rst::NativeArrayCommands::wireValue("transaction").toUtf8());
    rst::redoAction(&edit->action);
    require(equals(source, after) && rst::NativeArrayCommands::outcome("transaction").isEmpty(), "Adopted typed write preserves neighbors");
    require(!rst::NativeArrayCommands::outcome("transaction", true).isEmpty(), "Successful APPLY does not claim ROLLBACK consumption");
    const auto replayWire = rst::NativeArrayCommands::wireValue("transaction").toUtf8();
    auto duplicate = command(source, replayWire);
    rst::redoAction(&duplicate->action);
    rst::undoAction(&duplicate->action);
    require(equals(source, after) && delegatedUndo == 0 && delegatedRedo == 0, "Duplicate command before release never reaches native parser");
    rst::destroyCommand(duplicate.get(), 1);
    source.local = false;
    rst::undoAction(&edit->action);
    require(!source.local && !payload->apply.error.isEmpty(), "Identical inherited contents do not pass local-presence validation");
    source.local = true;
    for (int i = 0; i < 3; ++i) {
        rst::undoAction(&edit->action);
        require(equals(source, before), "Undo restores all bytes and trailing empty entry");
        rst::redoAction(&edit->action);
        require(equals(source, after), "Redo restores selected entry only");
    }
    auto rollback = command(source, rst::NativeArrayCommands::wireValue("transaction", true).toUtf8());
    rst::redoAction(&rollback->action);
    require(equals(source, before), "Rollback restores typed before state");
    require(rst::NativeArrayCommands::outcome("transaction", true).isEmpty(), "ROLLBACK completion is observed independently");
    rst::undoAction(&rollback->action);
    rst::undoAction(&edit->action);
    require(equals(source, before), "Undo of failed transaction retains original local state");
    rst::redoAction(&edit->action);
    rst::redoAction(&rollback->action);
    require(equals(source, before), "Redo of failed transaction retains original local state");
    rst::NativeArrayCommands::release("transaction");
    auto lateDuplicate = command(source, replayWire);
    rst::redoAction(&lateDuplicate->action);
    rst::undoAction(&lateDuplicate->action);
    require(equals(source, before) && delegatedUndo == 0 && delegatedRedo == 0, "Released wire token remains owned and inert");
    rst::destroyCommand(lateDuplicate.get(), 1);
    payload.reset();
    require(!lifetime.expired(), "History owns payload after request release");
    rst::destroyCommand(edit.get(), 5);
    require(!lifetime.expired() && lastDestroyFlags == 5, "Other adopted command retains payload and destructor flags pass through");
    rst::destroyCommand(rollback.get(), 1);
    require(lifetime.expired() && destroyed == 4, "History disposal releases typed payload exactly once");

    auto secondAfter = before;
    secondAfter[1] = "SecondNamedEntity";
    auto reusedPayload = plan(source, before, secondAfter, "transaction");
    const auto freshWire = rst::NativeArrayCommands::wireValue("transaction").toUtf8();
    require(freshWire != replayWire, "Reused receipt receives a new preparation nonce");
    auto staleReuse = command(source, replayWire);
    rst::redoAction(&staleReuse->action);
    require(equals(source, before) && !rst::NativeArrayCommands::outcome("transaction").isEmpty(), "Old delayed command cannot consume reused receipt");
    auto freshReuse = command(source, freshWire);
    rst::redoAction(&freshReuse->action);
    require(equals(source, secondAfter) && rst::NativeArrayCommands::outcome("transaction").isEmpty(), "New preparation still adopts its own command");
    rst::undoAction(&freshReuse->action);
    rst::NativeArrayCommands::release("transaction");
    rst::destroyCommand(staleReuse.get(), 1);
    rst::destroyCommand(freshReuse.get(), 1);

    auto failurePayload = plan(source, before, after, "failure");
    auto failure = command(source, rst::NativeArrayCommands::wireValue("failure").toUtf8());
    failNextCopy = true;
    rst::redoAction(&failure->action);
    require(equals(source, before) && !failurePayload->apply.error.isEmpty(), "Partial copy failure restores original typed array");
    rst::undoAction(&failure->action);
    rst::redoAction(&failure->action);
    require(equals(source, before), "Failed initial command cannot later apply through Redo");
    rst::NativeArrayCommands::release("failure");
    rst::destroyCommand(failure.get(), 1);

    const QStringList unrelated{"changed neighbor", "old"};
    rst::writeArray(&source, 0, unrelated, true);
    auto refusedPayload = plan(source, before, after, "stale");
    auto refused = command(source, rst::NativeArrayCommands::wireValue("stale").toUtf8());
    rst::redoAction(&refused->action);
    require(equals(source, unrelated) && !refusedPayload->apply.error.isEmpty(), "Stale snapshot refuses without writing wire token");
    rst::writeArray(&source, 0, before, true);
    rst::redoAction(&refused->action);
    rst::undoAction(&refused->action);
    require(equals(source, before) && delegatedUndo == 0 && delegatedRedo == 0, "Refused command stays inert across later history operations");
    rst::NativeArrayCommands::release("stale");
    rst::destroyCommand(refused.get(), 1);

    auto ordinary = command(source, "ordinary authored text");
    rst::redoAction(&ordinary->action);
    rst::undoAction(&ordinary->action);
    require(delegatedUndo == 1 && delegatedRedo == 1, "Unrelated commands delegate unchanged");
    rst::destroyCommand(ordinary.get(), 1);
    clearArray(source.values);
    require(state.commands.isEmpty() && state.pending.isEmpty() && state.receipts.isEmpty() && allocations == 0,
        "All retained commands, preparations and native strings released");
    return 0;
}
