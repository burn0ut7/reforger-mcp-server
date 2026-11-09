using System.Diagnostics;
using System.Runtime.InteropServices;
using System.ComponentModel;
var p=Process.GetProcessById(int.Parse(args[0]));
var tid=int.Parse(args[1]);
if(!p.Threads.Cast<ProcessThread>().Any(t=>t.Id==tid))throw new Exception("Wrong thread");
var modules=p.Modules.Cast<ProcessModule>().Select(m=>(Name:m.ModuleName,Base:(long)m.BaseAddress,Size:m.ModuleMemorySize)).ToArray();
string Locate(long addr){foreach(var m in modules)if(addr>=m.Base&&addr<m.Base+m.Size)return m.Name+"+0x"+(addr-m.Base).ToString("X");return "0x"+addr.ToString("X");}
var hp=N.OpenProcess(0x410,false,p.Id);var ht=N.OpenThread(0x4A,false,tid);
if(hp==0||ht==0)throw new Win32Exception();
var raw=Marshal.AllocHGlobal(1250);var ctx=(nint)(((long)raw+15)&~15L);var bytes=new byte[1232];var stack=new byte[4096];
try{for(int i=0;i<4;i++){
Marshal.Copy(new byte[1232],0,ctx,1232);Marshal.WriteInt32(ctx,48,0x100003);
if(N.SuspendThread(ht)==uint.MaxValue)throw new Win32Exception();
try{if(!N.GetThreadContext(ht,ctx))throw new Win32Exception();Marshal.Copy(ctx,bytes,0,1232);var rsp=(nint)BitConverter.ToInt64(bytes,152);if(!N.ReadProcessMemory(hp,rsp,stack,(nuint)stack.Length,out _))Array.Clear(stack);}
finally{if(N.ResumeThread(ht)==uint.MaxValue)throw new Win32Exception();}
Console.WriteLine("RIP "+Locate(BitConverter.ToInt64(bytes,248)));
Console.WriteLine("REGS "+string.Join(" ",new[]{120,128,136,144,152,160,168,176,184,192,200,208,216,224,232,240}.Select(o=>o+":"+Locate(BitConverter.ToInt64(bytes,o)))));
if(i==0){
 N.SymInitialize(hp,null,true);
 var frame=Marshal.AllocHGlobal(1024);Marshal.Copy(new byte[1024],0,frame,1024);
 Marshal.WriteInt64(frame,0,BitConverter.ToInt64(bytes,248));Marshal.WriteInt32(frame,12,3);
 Marshal.WriteInt64(frame,16,BitConverter.ToInt64(bytes,160));Marshal.WriteInt32(frame,28,3);
 Marshal.WriteInt64(frame,32,BitConverter.ToInt64(bytes,152));Marshal.WriteInt32(frame,44,3);
 var dbg=NativeLibrary.Load("dbghelp.dll");
 Console.WriteLine("UNWOUND STACK:");
 for(var k=0;k<60;k++){if(!N.StackWalk64(0x8664,hp,ht,frame,ctx,0,NativeLibrary.GetExport(dbg,"SymFunctionTableAccess64"),NativeLibrary.GetExport(dbg,"SymGetModuleBase64"),0))break;Console.WriteLine(Locate(Marshal.ReadInt64(frame,0)));}
 Marshal.FreeHGlobal(frame);N.SymCleanup(hp);
}
Console.WriteLine("Possible stack pointers (not unwound):");
for(int j=0;j<stack.Length;j+=8){var a=BitConverter.ToInt64(stack,j);var loc=Locate(a);if(!loc.StartsWith("0x"))Console.WriteLine(j.ToString("X")+" "+loc);}
Thread.Sleep(100);
}}
finally{Marshal.FreeHGlobal(raw);N.CloseHandle(ht);N.CloseHandle(hp);}
static class N{
[DllImport("dbghelp",SetLastError=true)]public static extern bool SymInitialize(nint process,string path,bool invade);
[DllImport("dbghelp")]public static extern bool SymCleanup(nint process);
[DllImport("dbghelp",SetLastError=true)]public static extern bool StackWalk64(uint m,nint p,nint t,nint frame,nint ctx,nint read,nint function,nint module,nint translate);
[DllImport("kernel32",SetLastError=true)]public static extern nint OpenProcess(uint a,bool b,int c);
[DllImport("kernel32",SetLastError=true)]public static extern nint OpenThread(uint a,bool b,int c);
[DllImport("kernel32",SetLastError=true)]public static extern uint SuspendThread(nint h);
[DllImport("kernel32",SetLastError=true)]public static extern uint ResumeThread(nint h);
[DllImport("kernel32",SetLastError=true)]public static extern bool GetThreadContext(nint h,nint p);
[DllImport("kernel32",SetLastError=true)]public static extern bool ReadProcessMemory(nint h,nint a,byte[] b,nuint n,out nuint r);
[DllImport("kernel32")]public static extern bool CloseHandle(nint h);
}
