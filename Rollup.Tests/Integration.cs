using AO.ConnectionStrings;
using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Dac;
using System.Data;
using System.Reflection;

namespace Rollup.Tests;

[TestClass]
public class Integration
{
	[TestMethod]
	public async Task StandardCase()
	{
		using var cn = Util.InitDatabase(
			"Rollup.Tests.Resources.RollupDemo.bacpac",
			"Server=(localdb)\\mssqllocaldb;Database=RollupDemo;Integrated Security=true");

		await CreateSampleSalesAsync(cn);


	}

	[TestMethod]
	public async Task SpayWiseDemo()
	{
		
	}

	#region SpayWise sample assets
	#endregion

	private Task CreateSampleSalesAsync(IDbConnection cn)
	{
		throw new NotImplementedException();
	}	
}