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
		using var cn = InitDatabase(
			"Rollup.Tests.Resources.RollupDemo.bacpac",
			"Server=(localdb)\\mssqllocaldb;Database=RollupDemo;Integrated Security=true");

		await CreateSampleSalesAsync(cn);


	}

	private Task CreateSampleSalesAsync(IDbConnection cn)
	{
		throw new NotImplementedException();
	}

	private static IDbConnection InitDatabase(string bacpacResource, string connectionString)
	{
		bool triedAlready = false;

		try_again:		

		try
		{
			var cn = new SqlConnection(connectionString);
			cn.Open();
			return cn;
		}
		catch 
		{
			if (triedAlready) throw;

			var dac = new DacServices(connectionString);
			var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(bacpacResource);
			using var bacpac = BacPackage.Load(stream);
			dac.ImportBacpac(bacpac, ConnectionString.Database(connectionString));
			triedAlready = true;
			goto try_again;
		}
	}
}