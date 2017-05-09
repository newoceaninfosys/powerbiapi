using DineConnect_PowerBI.Helpers;
using DineConnect_PowerBI.Services.Interfaces;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Configuration;
using System.Threading.Tasks;
using System.Web;

namespace DineConnect_PowerBI.Services.Services
{
    public class CacheSettingService : ISettingService
    {
        private readonly string _tenantId = ConfigurationManager.AppSettings["TenantId"].ToString();
        private readonly string _clientId = ConfigurationManager.AppSettings["ClientId"].ToString();
        private readonly string _clientSecrect = ConfigurationManager.AppSettings["ClientSecret"].ToString();

        public async Task<string> GetAccessToken()
        {
            var accessToken = CacheHelper.Get<string>("powerbi_accesstoken");
            var expiresOn = CacheHelper.Get<string>("powerbi_expiredon");
            var refreshToken = CacheHelper.Get<string>("powerbi_refreshtoken");

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
            CacheHelper.Clear();
        }

        public void SavePowerBIAccessToken(string accessToken, string expiredOn, string refreshToken)
        {
            CacheHelper.Set("powerbi_accesstoken", accessToken, 120);
            CacheHelper.Set("powerbi_expiredon", expiredOn, 120);
            CacheHelper.Set("powerbi_refreshtoken", refreshToken, 120);
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