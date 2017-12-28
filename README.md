# infer-dot-net

.NET Core port of [Infer.NET](http://research.microsoft.com/infernet).

### Build

Run followings commands to build DDL's.

```bash
git clone https://github.com/All-less/infer-dot-net.git
cd infer-dot-net/CommandLine
dotnet build
```

### Get Started

Execute following command in `CommandLine` folder.

```bash
dotnet run
```

### Known Issues

1. `Train` module of `Recommender` will throw `SerializationException`. See [this issue](https://github.com/dotnet/corefx/issues/23213) for the explanation.
