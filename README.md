# Z3.Linq

`.NET 6.0` LINQ bindings for the [Z3 theorem prover](https://github.com/Z3Prover/z3) from [Microsoft Research](https://www.microsoft.com/en-us/research/).

## History

2009: Bart De Smet describes a prototype LINQ to Z3 binding in three blog posts:

* [LINQ to Z3 - Part 1 – Exploring The Z3 Theorem Prover](docs/blogs/part-01-exploring-the-z3-theorem-prover.md)
* [LINQ to Z3 - Part 2 – LINQ to the Unexpected](docs/blogs/part-02-linq-to-the-unexpected.md)
* [LINQ to Z3 - Part 3 – Theorem Solving On Steroids](docs/blogs/part-03-theorem-solving-on-steroids.md)

2010: Bart was [interviewed on Channel 9](https://vimeo.com/648767290) about the LINQ to Z3:

[![LINQ to Z3 Channel 9 interview](docs/blogs/images/linq-to-z3-channel9.jpg)](https://vimeo.com/648767290)

2012: Bart presented [LINQ to Everything](https://vimeo.com/648776168) at TechEd Europe 2012:

[![LINQ to Everything](docs/blogs/images/linq-to-constraints.jpg)](https://vimeo.com/648776168)

2015: Z3 was open sourced under the MIT license and the [source code was moved to GitHub](https://github.com/Z3Prover/z3), where it is actively maintained.

2015: [Ricardo Niepel](https://github.com/RicardoNiepel) (Microsoft) publishes the sample as [Z3.LinqBinding](https://github.com/RicardoNiepel/Z3.LinqBinding) using `.NET 4.5` and Z3 binaries `4.4.0`

2018: [Jean-Sylvain Boige](https://github.com/jsboige) ([My Intelligence Agency](https://github.com/MyIntelligenceAgency)) adds [Missionaries And Cannibals sample](https://github.com/MyIntelligenceAgency/Z3.LinqBinding).

2020: [Karel Frajtak](https://github.com/kfrajtak) adds [support for fractions](https://github.com/kfrajtak/Z3.LinqBinding).

2021: [Howard van Rooijen](https://github.com/HowardvanRooijen) and [Ian Griffiths](https://github.com/idg10) ([endjin](https://github.com/endjin)) upgrade the project to `.NET 6.0` (adding [ValueTuple](https://docs.microsoft.com/en-us/dotnet/api/system.valuetuple?view=net-6.0) support and demonstrate using `record` types) and `Z3 v4.4.0`, merge in features from [Jean-Sylvain Boige](https://github.com/jsboige) and [Karel Frajtak](https://github.com/kfrajtak) forks, create archives of Bart's original blog posts and talks. They republish the project as [Z3.Linq](https://github.com/endjin/Z3.Linq), create a new [.NET Interactive Notebook](https://github.com/dotnet/interactive) of [samples](examples/z3-problems.dib), and publish a nuget package [Z3.Linq](https://www.nuget.org/packages/Z3.Linq/).