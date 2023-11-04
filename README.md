# Problem Statement
Imagine you have one or more reporting tables in SQL Server that need to be incrementally updated as changes happen elsehwere in your application. These reporting tables might have calculations that are too slow to generate on demand. You need a way to keep your reporting tables updated reliably. SQL Server [change tracking](https://learn.microsoft.com/en-us/sql/relational-databases/track-changes/about-change-tracking-sql-server?view=sql-server-ver16) is a good way to do this, but it's a complex feature with several moving pieces. It would be nice to have a C# interface that encapsulates as much of the details as possible, letting me focus on the results.

# Solution
This library acts as a wrapper around the SQL Server change tracking feature, with the intent to help you manage reporting rollup tables. I did something like this a while back with [ViewMaterializer](https://github.com/adamfoneil/ViewMaterializer) and [here](https://github.com/adamfoneil/SqlServerUtil/wiki/Using-ViewMaterializer), but I wanted to take a fresh look at the problem and incorporate stuff I've learned since then. This library is a more complete solution with working tests.

There are several things to unpack in this library.
- See the [integration test](https://github.com/adamfoneil/Rollup/blob/master/Rollup.Tests/Integration.cs) which is based on random, hypothetical [sales data](https://github.com/adamfoneil/Rollup/blob/master/Rollup.Tests/Entities/DetailSalesRow.cs). Specifically, see the [assertion](https://github.com/adamfoneil/Rollup/blob/master/Rollup.Tests/Integration.cs#L64) that the rollup data matches the live query results had we not used a rollup.
- See [SampleRollup](https://github.com/adamfoneil/Rollup/blob/master/Rollup.Tests/SampleRollup.cs) which queries the source data and executes the rollup. The hardest part about developing a rollup is working out this query, as required by the [QueryChangesAsync](https://github.com/adamfoneil/Rollup/blob/master/Rollup/Rollup.cs#L74) abstract method. I have a walkthrough on this below.

Low-level stuff:
- The [Rollup](https://github.com/adamfoneil/Rollup/blob/master/Rollup/Rollup.cs) class is the heart of this. This is what tracks the change tracking version number incremented by SQL Server. When you query the `CHANGETABLE` function, you pass a `@sinceVersion` param, and this class is responsible for tracking that parameter.
- In addition, `Rollup` has a nested abstract class [Table](https://github.com/adamfoneil/Rollup/blob/master/Rollup/Rollup.cs#L68) which represents a specific rollup table in your application. You create an instance of this for each rollup target. Example [SalesTable](https://github.com/adamfoneil/Rollup/blob/master/Rollup.Tests/SampleRollup.cs#L24).
- There are also some unique [Dapper extension methods](https://github.com/adamfoneil/Rollup/blob/master/Rollup/Extensions/DbConnectionExtensions.cs) that make it easy to work with json in SQL, taking advantage of the T-SQL `OPENJSON` function, which is helpful when working with table-value parameters.

# What about [indexed views](https://learn.microsoft.com/en-us/sql/relational-databases/views/create-indexed-views?view=sql-server-ver16)?
Indexed views are a built-in solution for this problem. For whatever reason, I have not had good results with indexed views -- meaning the couple times I tried to use them, they weren't very fast, and I had general trouble working with them. That's why I wanted a solution based on ordinary tables.

# Executing
Once you've created your `Rollup` and one or more `Table` instances, your application needs to call the [Rollup.ExecuteAsync](https://github.com/adamfoneil/Rollup/blob/master/Rollup/Rollup.cs#L30) method at some regular interval within your configured change tracking [retention period](https://learn.microsoft.com/en-us/sql/relational-databases/track-changes/about-change-tracking-sql-server?view=sql-server-ver16#change-tracking-cleanup). (I believe the default is 3 days.) You can call this from a button click in your application, for example. Or, you can setup a cronjob to execute the rollup on a defined interval. I use [BackgroundService.Extensions](https://github.com/adamfoneil/BackgroundService.Extensions) for this.

# Rollup query walkthrough
The core of your rollup is an implementation of the [QueryChangesAsync](https://github.com/adamfoneil/Rollup/blob/master/Rollup/Rollup.cs#L74) abstract method. Here's a guide on how to approach this.

First, think of your rollup table as a combination of one or more **dimension** columns along with one or more **fact** columns. In my [SalesRollup](https://github.com/adamfoneil/Rollup/blob/master/Rollup.Tests/Entities/SalesRollup.cs) example, the dimension columns are [Region, ItemType, and Year](https://github.com/adamfoneil/Rollup/blob/master/Rollup.Tests/Entities/SalesRollup.cs#L6-L8). There's only one fact column [Total](https://github.com/adamfoneil/Rollup/blob/master/Rollup.Tests/Entities/SalesRollup.cs#L12). Once you keep this distinction in mind, your query will fall into place.

1. Start with a CTE that returns just your dimensions that have changed since the last merge. This should include the `CHANGETABLE` and `@sinceVersion` part. This part does not include any fact data because the `CHANGETABLE` join limits what fact rows are included. If we got the fact data here, it would include only what has changed since the last merge, which would be incomplete.

```sql
WITH [dimensions] AS (
  SELECT
    [s].[RegionId],
    [i].[Type] AS [ItemType],						
    YEAR([s].[Date]) AS [Year]
  FROM
    CHANGETABLE(changes [dbo].[DetailSalesRow], @sinceVersion) [c]
    INNER JOIN [dbo].[DetailSalesRow] [s] ON [c].[Id]=[s].[Id]
    INNER JOIN [dbo].[Item] [i] ON [s].[ItemId]=[i].[Id]
    INNER JOIN [dbo].[Region] [r] ON [s].[RegionId]=[r].[Id]
  GROUP BY
    [s].[RegionId],
    [i].[Type],
    YEAR([s].[Date])
) 
```
2. Now query your fact table(s), joining to the CTE, and including any other joins needed. The `dimensions` CTE tells us what "buckets" of data have changed. Now we need to query the entire "bucket" of data from the fact table. Notice I join to an `Item` table because my rollup is at the `ItemType` level -- not individual items.

```sql
SELECT
  [r].[Name] AS [Region],
  [dim].[ItemType],
  [dim].[Year],
  SUM([Price]) AS [Total]
FROM
  [dbo].[DetailSalesRow] [fact]
  INNER JOIN [dbo].[Item] [i] ON [fact].[ItemId]=[i].[Id]
  INNER JOIN [dbo].[Region] [r] ON [fact].[RegionId]=[r].[Id]
  INNER JOIN [dimensions] [dim] ON
    [fact].[RegionId]=[dim].[RegionId] AND
    [i].[Type]=[dim].[ItemType] AND
    YEAR([fact].[Date])=[dim].[Year]
GROUP BY
  [r].[Name],
  [dim].[ItemType],
  [dim].[Year]
```
