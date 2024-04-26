# dotnet9-wasi-http-example

## Building

Currently, this only builds on Windows.  Linux support coming soon(ish).

- Git-Bash
- curl
- dotnet 9.0.100-preview.3.24204.13 or later
- Emscripten SDK 3.1.47 or later (until https://github.com/WebAssembly/wasi-sdk/issues/326 has been addressed)
- WASI SDK 21 or later
- Spin 2.4.x or later
- wit-bindgen from this PR branch: https://github.com/bytecodealliance/wit-bindgen/pull/939
- wasm-tools 1.206.0 or later

Once you have all of the above, something like this should build and run the app in Git-Bash:

```
export WASI_SDK_PATH=$(cygpath -w /path/to/wasi-sdk)
eval `EMSDK_BASH=1 python /path/to/emsdk/emsdk.py construct_env`
spin build -u
```
