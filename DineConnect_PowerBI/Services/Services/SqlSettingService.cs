using DineConnect_PowerBI.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Configuration;
using DineConnect_PowerBI.Helpers;
using RestSharp;
using Newtonsoft.Json.Linq;

namespace DineConnect_PowerBI.Services.Services
{
    public class SqlSettingService : ISettingService
    {
        private readonly string _tenantId = ConfigurationManager.AppSettings["TenantId"].ToString();
        private readonly string _clientId = ConfigurationManager.AppSettings["ClientId"].ToString();
        private readonly string _clientSecrect = ConfigurationManager.AppSettings["ClientSecret"].ToString();

        public async Task<string> GetAccessToken()
        {
            var accessToken = "";
            var expiresOn = "";
            var refreshToken = "";

            var conStr = ConfigurationManager.ConnectionStrings["Default"].ToString();
            var cmdStr = $"SELECT * FROM Settings";
            using (var con = new SqlConnection(conStr))
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
                        else
                            refreshToken = reader["Value"]?.ToString();
                    }
                }
            }

            double expiresOnTs;
            //if data save as invalid
            if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken) || !double.TryParse(expiresOn, out expiresOnTs))
                return "0_Access Token expired";

            //token not expired yet
            if (DateTimeHelper.ToDatetime(expiresOnTs) > DateTime.UtcNow)
                return accessToken;

            //token expired, try to refresh token
            var result = await RefreshingAccessToken(refreshToken);
            if (result.Status != 1)
                return "0_" + result.Message;

            return result.AccessToken;
        }

        public void ResetData()
        {
            var conStr = ConfigurationManager.ConnectionStrings["Default"].ToString();
            var cmdstr = "UPDATE Settings SET Value = ''";
            using (var con = new SqlConnection(conStr))
            {
                var cmd = new SqlCommand(cmdstr, con);
                con.Open();
                var res = cmd.ExecuteNonQuery();
            }
        }

        public void SavePowerBIAccessToken(string accessToken, string expiredOn, string refreshToken)
        {
            var conStr = ConfigurationManager.ConnectionStrings["Default"].ToString();

            var updateAccessToken = $"UPDATE Settings SET Value = '{accessToken}' WHERE Name = 'powerbi_accesstoken' ";
            var updateExpiredOn = $"UPDATE Settings SET Value = '{expiredOn}' WHERE Name = 'powerbi_expiredon' ";
            var updateRefreshToken = $"UPDATE Settings SET Value = '{refreshToken}' WHERE Name = 'powerbi_refreshtoken' ";
            var cmdstr = updateAccessToken + updateExpiredOn + updateRefreshToken;

            using (var con = new SqlConnection(conStr))
            {
                var cmd = new SqlCommand(cmdstr, con);
                con.Open();
                var res = cmd.ExecuteNonQuery();
            }
        }

        private async Task<dynamic> RefreshingAccessToken(string refreshToken)
        {
            try
            {
                var paramStr =
                    "client_id=" + HttpUtility.UrlEncode(_clientId) +
                    "&refresh_token=" + refreshToken +
                    "&grant_type=refresh_token" +
                    "&resource=https://analysis.windows.net/powerbi/api" +
                    "&client_secret=" + HttpUtility.UrlEncode(_clientSecrect);

                //prepare request
                var request = new RestRequest(Method.POST);
                request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
                request.AddParameter("application/x-www-form-urlencoded", paramStr, ParameterType.RequestBody);

                //execute request
                var url = $"https://login.windows.net/{_tenantId}/oauth2/token";
                var client = new RestClient(url);
                var response = client.Execute(request);
                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                    return await Task.FromResult(new { Status = 0, Message = response.StatusDescription });

                //save db and return
                var resContent = JObject.Parse(response.Content);
                SavePowerBIAccessToken(resContent["access_token"].ToString(), resContent["expires_on"].ToString(), resContent["refresh_token"].ToString());
                return await Task.FromResult(new { Status = 1, Message = "success", AccessToken = resContent["access_token"] });
            }
            catch (Exception ex)
            {
                return await Task.FromResult(new { Status = -1, Message = ex.ToString() });
            }
        }
    }
}