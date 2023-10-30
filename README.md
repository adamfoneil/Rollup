# Problem Statement
Imagine you have one or more reporting tables in SQL Server that need to be incrementally updated as changes happen elsehwere in your application. These reporting tables might have calculations that are too slow to generate on demand. You need a way to keep your reporting tables updated reliably. SQL Server [change tracking](https://learn.microsoft.com/en-us/sql/relational-databases/track-changes/about-change-tracking-sql-server?view=sql-server-ver16) is a good way to do this, but it's a complex feature with several moving pieces. It would be nice to have a C# interface that encapsulates as much of the details as possible, letting me focus on the results.

# Solution
This library acts as a wrapper around the SQL Server change tracking feature, with the intent to help you manage reporting rollup tables. I did something like this a while back with [ViewMaterializer](https://github.com/adamfoneil/ViewMaterializer), but I wanted to take a fresh look at the problem and incorporate stuff I've learned since then. This library is a more complete solution with working tests.

There are several things to unpack in this library.
- See the [integration test](https://github.com/adamfoneil/Rollup/blob/master/Rollup.Tests/Integration.cs) which is based on random, hypothetical [sales data](https://github.com/adamfoneil/Rollup/blob/master/Rollup.Tests/Entities/DetailSalesRow.cs). Specically, see the [assertion](https://github.com/adamfoneil/Rollup/blob/master/Rollup.Tests/Integration.cs#L55) that the rollup data matches the live query results had we not used a rollup.
- See [SampleRollup](https://github.com/adamfoneil/Rollup/blob/master/Rollup.Tests/SampleRollup.cs) which queries the source data and executes the rollup. This class has two queries, one that queries a `CHANGETABLE` for information on rows that have changed since the last merge. The second query gets the "report facts" (sums or other aggregates) that go along with those modified rows.

Low-level stuff:
- The [Rollup](https://github.com/adamfoneil/Rollup/blob/master/Rollup/Rollup.cs) class is the heart of this.
- There are also some unique [Dapper extension methods](https://github.com/adamfoneil/Rollup/blob/master/Rollup/Extensions/DbConnectionExtensions.cs) that make it easy to work with json in SQL, taking advantage of the T-SQL `OPENJSON` function, which is helpful when working with table-value parameters.

# What about [indexed views](https://learn.microsoft.com/en-us/sql/relational-databases/views/create-indexed-views?view=sql-server-ver16)?
Indexed views are a built-in solution for this problem. For whatever reason, I have not had good results with indexed views -- meaning the couple times I tried to use them, they weren't very fast, and I had general trouble working with them. That's why I wanted a solution based on ordinary tables.
