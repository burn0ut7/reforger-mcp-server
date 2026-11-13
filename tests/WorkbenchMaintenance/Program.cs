using System.Runtime.InteropServices;
using System.Text;
using ReforgerMcp;

if (args.Length > 0) {
    Environment.ExitCode = args[0] is "--extension-live" or "--extension-choices-live" ? await LiveExtension.Run(args) : await LiveMaintenance.Run(args);
    return;
}
PlayCallbackChecks.Run();
RuntimeSetupChecks.Run();
WorldCallbackChecks.Run();
ExtensionCallbackChecks.Run();
ArrayAddCallbackChecks.Run();
PickerCallbackChecks.Run();
FinishEditCallbackChecks.Run();

var allocations = new List<nint>();
nint Alloc(int size) { var p = Marshal.AllocHGlobal(size); Marshal.Copy(new byte[size],0,p,size); allocations.Add(p); return p; }
nint Text(string value, int references = 1) {
    var bytes = Encoding.UTF8.GetBytes(value); var p = Alloc(bytes.Length + 13);
    Marshal.WriteInt32(p,4,bytes.Length); Marshal.WriteInt32(p,8,references);
    Marshal.Copy(bytes,0,p+12,bytes.Length); return p+12;
}
void Pointer(nint p, int offset, nint value) => Marshal.WriteIntPtr(p,offset,value);
var image = Win.VirtualAlloc(0,0x2100000,0x3000,0x40);
if(image == 0) throw new Exception("Cannot allocate synthetic image");
var output = Alloc(4096 + 8192);
var owner = Alloc(512);
var addon = Alloc(128);
var array = Alloc(8);
Pointer(array,0,addon); Pointer(owner,0xc8,array); Pointer(owner,0xd8,array);
Marshal.WriteInt32(owner,0xd4,1); Marshal.WriteInt32(owner,0xe4,1);
foreach(var (offset,value) in new[]{(0x20,"Fixture"),(0x28,"0123456789ABCDEF"),(0x30,"Fixture title"),(0x38,"C:/Fixture/addon.gproj")}) Pointer(addon,offset,Text(value));
var callbacks = new List<Delegate>();
void Hook(int rva, Delegate callback) {
    callbacks.Add(callback); var target=Marshal.GetFunctionPointerForDelegate(callback);
    var bytes=new List<byte>{0x48,0xb8}; bytes.AddRange(BitConverter.GetBytes((long)target)); bytes.AddRange(new byte[]{0xff,0xe0});
    Marshal.Copy(bytes.ToArray(),0,image+rva,bytes.Count);
}
int frees=0, resolves=0, count=0;
string mount="C:/Fixture/";
int references=1;
var returned = new List<nint>();
Owner getOwner=()=>owner;
Resolve resolve=(path,destination,absolute,normalize,index)=>{
    if(Marshal.PtrToStringUTF8(path)!="$Fixture:" || absolute!=0 || normalize!=0 || index!=-1) throw new Exception("Resolver ABI mismatch");
    resolves++;
    var text=Text(mount,references); returned.Add(text); Marshal.WriteIntPtr(destination,text); return 1;
};
Free free=(allocation,unused)=>{
    if(unused!=0 || !returned.Contains(allocation+12) || Marshal.ReadInt32(allocation,8)!=0) throw new Exception("String release ABI mismatch");
    frees++;
};
Hook(0x1f07120,getOwner); Hook(0x2019c0,resolve); Hook(0x1e95e70,free);
var code=NativeWorkbench.ProjectContextProbe(image,NativeWorkbench.MaintenanceProfile.Current);
Marshal.Copy(code,0,image,code.Length);
if(!Win.FlushInstructionCache(Win.GetCurrentProcess(),image,0x2100000)) throw new Exception("Flush failed");
var probe=Marshal.GetDelegateForFunctionPointer<Probe>(image);
void Check(bool ok,string test) { if(!ok)throw new Exception(test); count++; Console.WriteLine("PASS "+test); }
void Run() { Marshal.Copy(new byte[4096+8192],0,output,4096+8192); frees=0;resolves=0;returned.Clear();probe(output); }
try {
    Check(NativeWorkbench.MaintenanceProfile.FromHash(NativeWorkbench.MaintenanceProfile.Current.Hash).Limited,"1.9 limited profile");
    Check(!NativeWorkbench.MaintenanceProfile.FromHash(NativeWorkbench.MaintenanceProfile.Legacy.Hash).Limited,"legacy profile retained");
    var rejected=false;try{NativeWorkbench.MaintenanceProfile.FromHash(new string('0',64));}catch(InvalidOperationException){rejected=true;}
    Check(rejected,"unknown binary rejected");
    Check(NativeWorkbench.MaintenanceProfile.Current.SupportsUi("close") && NativeWorkbench.MaintenanceProfile.Current.SupportsUi("invoke"),"1.9 pinned Qt lifecycle actions admitted");
    Check(NativeWorkbench.MaintenanceProfile.Current.SupportsUi("add_item") && !NativeWorkbench.MaintenanceProfile.Current.SupportsUi("unknown"),"1.9 array owner admitted; unknown UI actions rejected");
    Check(NativeWorkbench.MaintenanceProfile.Current.Reload == 0x22ca80 && NativeWorkbench.MaintenanceProfile.Current.ReloadBlockedOffset == 0x1656,"1.9 owning reload and moved state offset");
    Run();
    Check(Marshal.ReadInt32(output,72)==2,"project callback completes");
    Check(Marshal.ReadInt32(output,104)==1 && Marshal.ReadInt32(output,108)==1,"loaded and available counts");
    Check(Marshal.PtrToStringUTF8(output+4096+832)=="C:/Fixture/addon.gproj","project path copied");
    Check(Marshal.PtrToStringUTF8(output+4096+2048)==mount && Marshal.PtrToStringUTF8(output+8192+2048)==mount,"owned string copied for both arrays");
    Check(resolves==2 && frees==2 && Marshal.ReadInt64(output+4096+3344)==0,"owned strings released exactly once");
    references=2; Run();
    Check(frees==0 && returned.All(p=>Marshal.ReadInt32(p,-4)==1),"shared strings retain other owner's reference");
    references=1; mount=new string('x',1024);Run();
    Check(Marshal.ReadInt32(output,72)==28 && frees==1 && resolves==1,"oversize rejects after release without visiting more rows");
    mount="";Run();
    Check(Marshal.ReadInt32(output,72)==2 && frees==2,"empty string cleanup");
    Marshal.WriteInt32(owner,0xd4,1025);Run();
    Check(Marshal.ReadInt32(output,72)==28 && resolves==0,"invalid native count rejected before resolve");
    Console.WriteLine($"{count} maintenance profile/callback checks passed.");
}
finally { GC.KeepAlive(callbacks); Win.VirtualFree(image,0,0x8000); foreach(var p in allocations)Marshal.FreeHGlobal(p); }
[UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate nint Owner();
[UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate byte Resolve(nint path,nint destination,byte absolute,byte normalize,int index);
[UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate void Free(nint allocation,int unused);
[UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate void Probe(nint output);
static class Win {
 [DllImport("kernel32")]internal static extern nint VirtualAlloc(nint p,nuint size,uint type,uint protect);
 [DllImport("kernel32")]internal static extern bool VirtualFree(nint p,nuint size,uint type);
 [DllImport("kernel32")]internal static extern bool FlushInstructionCache(nint process,nint address,nuint size);
 [DllImport("kernel32")]internal static extern nint GetCurrentProcess();
}

