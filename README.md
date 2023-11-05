# Problem Statement
Imagine you have one or more reporting tables in SQL Server that need to be incrementally updated as changes happen elsehwere in your application. These reporting tables might have calculations that are too slow to generate on demand. You need a way to keep your reporting tables updated reliably. SQL Server [change tracking](https://learn.microsoft.com/en-us/sql/relational-databases/track-changes/about-change-tracking-sql-server?view=sql-server-ver16) is a good way to do this, but it's a complex feature with several moving pieces. It would be nice to have a C# interface that encapsulates as much of the details as possible, letting me focus on the results.

# Solution
This library acts as a wrapper around the SQL Server change tracking feature, with the intent to help you manage reporting rollup tables. I did something like this a while back with [ViewMaterializer](https://github.com/adamfoneil/ViewMaterializer) and [here](https://github.com/adamfoneil/SqlServerUtil/wiki/Using-ViewMaterializer), but I wanted to take a fresh look at the problem and incorporate stuff I've learned since then. This library is a more complete solution with working tests.

There are several things to unpack in this library.
- See the [integration test](https://github.com/adamfoneil/Rollup/blob/master/Rollup.Tests/Integration.cs) which is based on random, hypothetical [sales data](https://github.com/adamfoneil/Rollup/blob/master/Rollup.Tests/Entities/DetailSalesRow.cs). Specifically, see the [assertion](https://github.com/adamfoneil/Rollup/blob/master/Rollup.Tests/Integration.cs#L72) that the rollup data matches the live query results had we not used a rollup.
- See [SampleRollup](https://github.com/adamfoneil/Rollup/blob/master/Rollup.Tests/SampleRollup.cs) which queries the source data and executes the rollup. The hardest part about developing a rollup is working out this query, as required by the [QueryChangesAsync](https://github.com/adamfoneil/Rollup/blob/master/Rollup/Rollup.cs#L74) abstract method. I have a walkthrough on this below.

Low-level stuff:
- The [Rollup](https://github.com/adamfoneil/Rollup/blob/master/Rollup/Rollup.cs) class is the heart of this. This is what tracks the change tracking version number incremented by SQL Server. When you query the `CHANGETABLE` function, you pass a `@sinceVersion` param, and this class is responsible for tracking that parameter.
- In addition, `Rollup` has a nested abstract class [Table](https://github.com/adamfoneil/Rollup/blob/master/Rollup/Rollup.cs#L79) which represents a specific rollup table in your application. You create an instance of this for each rollup target. Example [SalesTable](https://github.com/adamfoneil/Rollup/blob/master/Rollup.Tests/SampleRollup.cs#L26).
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

# Deletions
The approach I've outlined so far doesn't handle deletions. The problem is that although the CHANGETABLE function will include deletions in its results, a join to your fact table won't return any rows -- precisely because those fact rows have been deleted! In the example above, this refers to this part of the query:

```sql
-- returns inserts and updates only (not deletions)
CHANGETABLE(changes [dbo].[DetailSalesRow], @sinceVersion) [c]
INNER JOIN [dbo].[DetailSalesRow] [s] ON [c].[Id]=[s].[Id]
```

To address this, I recommend this:
- In your rollup table(s) add an `IsModified` bit column. (Call it whatever you like, but it should be a `bit` and not part of the key.)
- Create a delete trigger on your fact table that sets `IsModified = 1` for impacted dimensions. It's sort of a shame that the the point of Change Tracking was to avoid managing your own triggers, but I don't know a better way here.
- In your rollup changes query (implemented by `Table.QueryChangesAsync`), add a `UNION` to a query that checks for rollup rows where `IsModified = 1`. That way dimensions affected by a deletion will be included in the rollup merge.

Here's an example from an application where I implemented this:

<details>
  <summary>Delete trigger on fact table</summary>

```sql
ALTER TRIGGER [dbo].[TR_Transaction_Delete] ON [dbo].[Transaction]
FOR DELETE
AS
UPDATE [rev] SET
    [IsModified]=1
FROM
    [deleted] [del]
    LEFT JOIN [dbo].[VolumeClient] [vc] ON [del].[ClientId] = [vc].[ClientId]
    INNER JOIN [report].[Revenue] [rev] ON 
        [del].[ClinicId] = [rev].[ClinicId] AND
        [del].[Date] = [rev].[Date] AND
        CASE WHEN [vc].[Id] IS NULL THEN 0 ELSE 1 END = [rev].[ClientType]
```

</details>

<details>
  <summary>QueryChangesAsync query</summary>
  
  ```sql
-- this top query picks up inserts and updates
WITH [dimensions] AS (
    SELECT
        [txn].[ClinicId],
        [txn].[Date],
        CASE WHEN [vc].[Id] IS NOT NULL THEN 1 ELSE 0 END AS [ClientType]
    FROM
        CHANGETABLE(changes [dbo].[Transaction], @sinceVersion) [c]
        INNER JOIN [dbo].[Transaction] [txn] ON [c].[Id] = [txn].[Id]
        LEFT JOIN [dbo].[VolumeClient] [vc] ON [txn].[ClientId] = [vc].[ClientId]
        INNER JOIN [app].[TransactionType] [tt] ON [txn].[TypeId] = [tt].[Id]
    WHERE
        [tt].[DepositMultiplier]<>0
    GROUP BY
        [txn].[ClinicId],
        [txn].[Date],
        CASE WHEN [vc].[Id] IS NOT NULL THEN 1 ELSE 0 END
) SELECT
    [dims].[ClinicId],
    [dims].[Date],
    [dims].[ClientType],
    SUM([txn].[Amount]*[tt].[DepositMultiplier]) AS [Amount]
FROM
    [dbo].[Transaction] [txn]
    LEFT JOIN [dbo].[VolumeClient] [vc] ON [txn].[ClientId] = [vc].[ClientId]
    INNER JOIN [app].[TransactionType] [tt] ON [txn].[TypeId] = [tt].[Id]
    INNER JOIN [dimensions] [dims] ON            
        [dims].[ClinicId] = [txn].[ClinicId] AND
        [dims].[Date] = [txn].[Date] AND
        [dims].[ClientType] = CASE WHEN [vc].[Id] IS NOT NULL THEN 1 ELSE 0 END
WHERE
    [tt].[DepositMultiplier]<>0
GROUP BY
    [dims].[ClinicId],
    [dims].[Date],
    [dims].[ClientType]

UNION

-- this query picks up deletions
SELECT
    [rev].[ClinicId],
    [rev].[Date],
    [rev].[ClientType],	
    COALESCE([t].[NetAmount], 0) AS [Amount]
FROM
    [report].[Revenue] [rev]
    LEFT JOIN (
        SELECT
            [txn].[ClinicId],
            [txn].[Date],
            CASE WHEN [vc].[Id] IS NOT NULL THEN 1 ELSE 0 END AS [ClientType],
            SUM([txn].[Amount]*[tt].[DepositMultiplier]) AS [NetAmount]
        FROM
            [dbo].[Transaction] [txn]
            INNER JOIN [app].[TransactionType] [tt] ON [txn].[TypeId]=[tt].[Id]
            LEFT JOIN [dbo].[VolumeClient] [vc] ON [txn].[ClientId]=[vc].[ClientId]
        WHERE
            [tt].[DepositMultiplier]<>0
        GROUP BY
            [txn].[ClinicId],
            [txn].[Date],
            CASE WHEN [vc].[Id] IS NOT NULL THEN 1 ELSE 0 END
    ) [t] ON
        [rev].[ClinicId]=[t].[ClinicId] AND
        [rev].[ClientType]=[t].[ClientType] AND
        [rev].[Date]=[t].[Date]
WHERE
    [rev].[IsModified]=1
```

</details>


# Troubleshooting
When I deployed my Rollup solution in a production app, I ran into discrepancies between my rollup reporting data and a separate report meant to validate the rollup data. This is something I'm in the middle of working through at the moment. To that end, I've added a some classes to help with debugging:
- [TotalsValidator](https://github.com/adamfoneil/Rollup/blob/master/Rollup/TotalsValidator.cs) used for asserting that rollup totals match a "live" query source. See my [SampleValidator](https://github.com/adamfoneil/Rollup/blob/master/Rollup.Tests/SampleValidator.cs) and its [test use](https://github.com/adamfoneil/Rollup/blob/master/Rollup.Tests/Integration.cs#L73-L75).
- [MismatchFinder](https://github.com/adamfoneil/Rollup/blob/master/Rollup/MismatchFinder.cs) used for finding mismtatched detail rows between a rollup data source and "live" query. See [SampleMismatchFinder](https://github.com/adamfoneil/Rollup/blob/master/Rollup.Tests/Mismatches.cs#L32) along with its [test](https://github.com/adamfoneil/Rollup/blob/master/Rollup.Tests/Mismatches.cs#L10)

Note that in my case, the root cause of discrepancies was improper delete handling, so see the section above on Deletions for more info.
