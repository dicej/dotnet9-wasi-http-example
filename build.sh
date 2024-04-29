set -eo pipefail

wit-bindgen c-sharp -w proxy -r native-aot wit

dotnet publish App.csproj \
       -r wasi-wasm \
       -c Release \
       -p:PlatformTarget=AnyCPU \
       -p:MSBuildEnableWorkloadResolver=false \
       --self-contained \
       -p:UseAppHost=false \
       -o target \
       | grep -v 'warning C'

if [ ! -e wasi_snapshot_preview1.proxy.wasm ]; then
    curl -LO https://github.com/bytecodealliance/wasmtime/releases/download/v20.0.0/wasi_snapshot_preview1.proxy.wasm
fi

wasm-tools component new --adapt wasi_snapshot_preview1.proxy.wasm target/csharp-wasm.wasm -o target/component.wasm
wasm-tools strip -a target/component.wasm -o target/stripped.wasm
