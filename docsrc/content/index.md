# What is Farkle?

Farkle is a parser generator for F#. It is made of an engine for the [GOLD Parsing system][gold], and an easy-to-use API to convert Abstract Syntax Trees into custom types.

<div class="row">
  <div class="span1"></div>
  <div class="span6">
    <div class="well well-small" id="nuget">
      The Farkle library can be <a href="https://nuget.org/packages/Farkle">installed from NuGet</a>:
      <pre>PM> Install-Package Farkle</pre>
    </div>
  </div>
  <div class="span1"></div>
</div>


## Documentation

The library comes with comprehensible documentation. 

 * [Quick Start](quickstart.html) to get started.

 * [API Reference](reference/index.html) contains automatically generated documentation for all types, modules and functions in the library. This includes additional brief samples on using most of the functions.

## Contributing and copyright

The project is hosted on [GitHub][gh] where you can [report issues][issues], fork 
the project and submit pull requests. If you're adding a new public API, please also 
consider adding [samples][content] that can be turned into a documentation.

The library is available under the MIT license, which allows modification and 
redistribution for both commercial and non-commercial purposes. For more information see the 
[License file][license] in the GitHub repository. 

  [content]: https://github.com/teo-tsirpanis/Farkle/tree/master/docs/content
  [gh]: https://github.com/teo-tsirpanis/Farkle
  [issues]: https://github.com/teo-tsirpanis/Farkle/issues
  [license]: https://github.com/teo-tsirpanis/Farkle/blob/master/LICENSE.txt
  [gold]: http://www.goldparser.org/