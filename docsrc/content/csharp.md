# Using Farkle from C\#

Farkle is a library written in F#, but supporting the much more used C# is a valuable feature. Because these two languages are quite different, there is a more idiomatic API for C# users. In this tutorial, we will assume that you have read [the F# quickstart guide][fsharp].

> The API described here is available in F# as well.

## Building grammars

F# programs using Farkle use an operator-ladden API to compose designtime Farkles. Because C# does not support custom operators, we can instead use a different API based on extension methods.

### Creating regexes

Regular expressions are created using [members of the `Regex` class][regex], which is well documented. Predefined sets are in the `PredefinedSets` class.

### Cerating & building designtime Farkles

The following table highlights the differences between the F# and C# designtime Farkle API.

|F#|C#|
|--|--|
|`terminal "X" (T (fun _ x -> x.ToString())) r`|`Terminal.Create("X", (position, data) => data.ToString(), r)`|
|`"S" ||= [p1; p2]`|`Nonterminal.Create("S", p1, p2)`|
|`!@ x`|`x.Extended()`|
|`!% x`|`x.Appended()`|
|`!& "literal"`|`"literal".Appended()`|
|`empty`|`ProductionBuilder.Empty`|
|`newline`|`Terminal.NewLine`|
|`x .>> y`|`x.Append(y)`|
|`x .>>. y`|`x.Extend(y)`|
|`x => (fun x -> MyFunc x)`|`x.Finish(x => MyFunc(x))`
|`x =% 0`|`x.FinishConstant(0)`|
|`RuntimeFarkle.build x`|`x.Build()`|

### Customizing designtime Farkles

To customize things like the case-sensitivity of designtime Farkles, there are some [extension methods for designtime Farkles][designtimeFarkleExtensions].

## Parsing

To parse text, there are some [extension methods for runtime Farkles][runtimeFarkleExtensions].

[fsharp]: quickstart.html
[regex]: reference/farkle-builder-regex.html
[designtimeFarkleExtensions]: reference/farkle-designtimefarkleextensions.html
[runtimeFarkleExtensions]: reference/farkle-runtimefarkleextensions.html