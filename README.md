# AI Anatomy

AI Anatomy is a C#/WPF teaching app that explains how transformers work. Every number it shows comes from a small transformer engine written from scratch in this repository, with no machine-learning libraries, so you can watch real tokens, attention and training happen step by step.

## Running the tests

The default run is fast and works in Debug; the slow induction-head training test is skipped there:

```
dotnet test AIAnatomy.slnx
```

To include the slow tests, run them in Release:

```
dotnet test AIAnatomy.slnx -c Release --filter Category=Slow
```

`tests/AIAnatomy.App.Tests` needs Windows: it checks that every lesson formula can be read and drawn by WpfMath, the formula renderer the app uses.
