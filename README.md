# dotnet9-wasi-http-example

## Building

Prerequisites:

- curl
- dotnet 9.0.100-preview.3.24204.13 or later
- WASI SDK 22
- Spin 2.4.x or later
- wit-bindgen 0.25.0 or later
- wasm-tools 1.206.0 or later

Once you have all of the above, something like this should build and run the app:

```
export WASI_SDK_PATH=/path/to/wasi-sdk
spin build -u
```
