using System.Runtime.CompilerServices;
using ProxyWorld.wit.imports.wasi.io.v0_2_0;

namespace ProxyWorld.wit.exports.wasi.http.v0_2_0;

internal static class WasiEventLoop
{
    internal static Task Register(IPoll.Pollable pollable)
    {
        var handle = pollable.Handle;
        pollable.Handle = 0;
        return CallRegister((Thread)null!, handle);

        [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "Register")]
        static extern Task CallRegister(Thread t, int handle);
    }

    internal static void Dispatch()
    {
        CallDispatch((Thread)null!);

        [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "Dispatch")]
        static extern void CallDispatch(Thread t);
    }
}
