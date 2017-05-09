using Microsoft.Owin;
using Owin;
using Autofac;
using DineConnect_PowerBI.Services.Interfaces;
using DineConnect_PowerBI.Services.Services;
using System.Web.Http;
using Autofac.Integration.WebApi;
using System.Configuration;
using System.Data.SqlClient;
using System;

[assembly: OwinStartup(typeof(DineConnect_PowerBI.Startup))]

namespace DineConnect_PowerBI
{
    public partial class Startup
    {
        public static IContainer container { get; set; }

        public void Configuration(IAppBuilder app)
        {
            var httpConfig = new HttpConfiguration();
            var builder = new ContainerBuilder();

            builder.RegisterApiControllers(typeof(WebApiApplication).Assembly);

            var useSql = ConfigurationManager.AppSettings.Get("UseSql");
            if (useSql == "1")
            {
                builder.RegisterType<SqlSettingService>()
                    .As<ISettingService>().InstancePerDependency();
                CheckDatabase();
            }
            else
            {
                builder.RegisterType<CacheSettingService>()
                    .As<ISettingService>().InstancePerDependency();
            }

            container = builder.Build();
            var resolver = new AutofacWebApiDependencyResolver(container);
            httpConfig.DependencyResolver = resolver;

            //ConfigureAuth(app, resolver);
            ConfigureAuth(app);
            app.UseAutofacMiddleware(container);
            app.UseAutofacWebApi(httpConfig);
            app.UseWebApi(httpConfig);
            WebApiConfig.Register(httpConfig);
        }

        private void CheckDatabase()
        {
            var accessToken = "";
            var expiresOn = "";
            var refreshToken = "";

            var conStr = ConfigurationManager.ConnectionStrings["Default"].ToString();
            var cmdStr = $"SELECT * FROM Settings";
            using (var con = new SqlConnection(conStr))
            {
                try
                {
                    var cmd = new SqlCommand(cmdStr, con);
                    con.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var name = reader["Name"].ToString();
                            if (name == "powerbi_accesstoken")
                                accessToken = reader["Value"]?.ToString();
                            else if (name == "powerbi_expiredon")
                                expiresOn = reader["Value"]?.ToString();
                            else if (name == "powerbi_refreshtoken")
                                refreshToken = reader["Value"]?.ToString();
                        }
                    }
                }
                catch (Exception ex)
                {
                }
            }

            double expiresOnTs;
            //if data save as invalid
            if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken) || !double.TryParse(expiresOn, out expiresOnTs))
            {
                using (var con = new SqlConnection(conStr))
                {
                    //var createDatabaseCmd = "CREATE DATABASE PowerBiSettings ";
                    var dropTable = "DROP TABLE Settings ";
                    var createTableCmd =
                        "CREATE TABLE [dbo].[Settings](" +
                        "[Id][nvarchar](100) NULL," +
                        "[Name] [nvarchar] (100) NULL," +
                        "[Value] [nvarchar] (2000) NULL" +
                        ") ON[PRIMARY] ";
                    var insertData =
                        $"INSERT INTO Settings (Id, Name, Value) VALUES " +
                        $"('0','powerbi_accesstoken','default_value')," +
                        $"('1','powerbi_expiredon','default_value')," +
                        $"('2','powerbi_refreshtoken','default_value')";
                    var cmdStr2 = dropTable + createTableCmd + insertData;
                    var cmd2 = new SqlCommand(cmdStr2, con);
                    con.Open();
                    var res = cmd2.ExecuteNonQuery();
                }
            }
        }

    }

}
