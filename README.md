# panoramovie
Panoramic Movie Recorder

# How to build
## Install Dotnet 6.0
Windows: https://docs.microsoft.com/ja-jp/dotnet/core/install/windows?tabs=net60   
macOS: https://docs.microsoft.com/ja-jp/dotnet/core/install/macos    
Linux: https://docs.microsoft.com/ja-jp/dotnet/core/install/linux   

Maybe, neither VisualStudio(Win), VisualStudio Code, VisualStudio for mac(macOS), XamarinStudio(Win/macOS), MonoDevelop(Win/Linux/BSD), nor SharpDevelop(Win/Linux/BSD) will be needed.

## Install Android Workload
Execute this command.
```sh
$ dotnet workload install android
```

## Rewrite Android SDK Path
Replace $(ANDROID_SDK) with the correct android sdk path in PanoraMovie.fsproj.
```xml
<AndroidSdkDirectory>$(ANDROID_SDK)</AndroidSdkDirectory>
```

## Build&Execute
Execute `dotnet run`.
