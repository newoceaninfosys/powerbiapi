using DineConnect_PowerBI.Helpers;
using DineConnect_PowerBI.Models;
using DineConnect_PowerBI.Services.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using RestSharp.Extensions.MonoHttp;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;

namespace DineConnect_PowerBI.Controllers
{
    [RoutePrefix("api/nois")]
    public class NoisController : ApiController
    {
        #region fields

        private readonly string tenantId = ConfigurationManager.AppSettings["TenantId"].ToString();
        private readonly string _clientId = ConfigurationManager.AppSettings["ClientId"].ToString();
        private readonly string _clientSecrect = ConfigurationManager.AppSettings["ClientSecret"].ToString();
        private readonly string _powerBIApi = ConfigurationManager.AppSettings["PowerBiApiUrl"].ToString();
        private readonly string _powerBiDataset = ConfigurationManager.AppSettings["PowerBiDatasetUrl"].ToString();
        private readonly string _authUrl = ConfigurationManager.AppSettings["AuthUrl"].ToString();
        private readonly string _redirectUrl = ConfigurationManager.AppSettings["RedirectUrl"].ToString();

        private readonly ISettingService _settingService;

        #endregion

        #region ctor
        public NoisController(ISettingService settingService)
        {
            _settingService = settingService;
        }
        #endregion

        #region private methods
        private async Task<bool> CheckResponseIsExpired(Dictionary<string, dynamic> response)
        {
            return await Task.FromResult(response.ContainsKey("error") && response["error"].code == "TokenExpired");
        }

        private async Task<Dictionary<string, dynamic>> GetListDatasetService(string accessToken)
        {
            var client = new RestClient(_powerBiDataset);
            var request = new RestRequest(Method.GET);
            request.AddHeader("Authorization", string.Format("Bearer {0}", accessToken));
            var response = client.Execute(request);
            var datasets = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(response.Content))
                return null;

            var json = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(response.Content);
            return await Task.FromResult(json);
        }

        private async Task<Dictionary<string, dynamic>> CreateDatasetService(string accessToken)
        {
            var json = new
            {
                name = $"NOIS_{DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")}",
                tables = new List<object>()
                            {
                                new
                                {
                                    name = "Product",
                                    columns = new List<object>()
                                    {
                                        new {name = "CreatedAt", dataType = "DateTime"},
                                        new {name = "Name", dataType = "String"},
                                        new {name = "Price", dataType = "Double"}
                                    }
                                }
                            }
            };
            var client = new RestClient(_powerBiDataset);
            var request = new RestRequest(Method.POST);
            request.AddHeader("Authorization", string.Format("Bearer {0}", accessToken));
            request.AddHeader("Content-Type", "application/json");
            request.AddParameter("application/json", JsonConvert.SerializeObject(json), ParameterType.RequestBody);
            var iRestResponse = client.Execute(request);
            var response = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(iRestResponse.Content);
            return await Task.FromResult(response);
        }

        private async Task<Dictionary<string, dynamic>> PushRowsService(string datasetId, string accessTokenRes)
        {
            var client = new RestClient(_powerBiDataset);
            var request = new RestRequest($"{datasetId}/tables/{"Product"}/rows", Method.POST);
            request.AddHeader("Authorization", string.Format("Bearer {0}", accessTokenRes));
            var rows = new List<object>
                        {
                            new { CreatedAt = DateTime.UtcNow ,Name = "Product_" +  Guid.NewGuid().ToString()},
                            new { CreatedAt = DateTime.UtcNow ,Name = "Product_" +  Guid.NewGuid().ToString()},
                            new { CreatedAt = DateTime.UtcNow ,Name = "Product_" +  Guid.NewGuid().ToString()},
                            new { CreatedAt = DateTime.UtcNow ,Name = "Product_" +  Guid.NewGuid().ToString()},
                            new { CreatedAt = DateTime.UtcNow ,Name = "Product_" +  Guid.NewGuid().ToString()},
                        };
            request.AddParameter("application/json", JsonConvert.SerializeObject(rows), ParameterType.RequestBody);
            var iRestResponse = client.Execute(request);
            var response = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(iRestResponse.Content);
            return await Task.FromResult(response);
        }
        #endregion

        #region methods
        [HttpGet]
        [Route("signin")]
        public IHttpActionResult SignInPowerBi()
        {
            var url = _authUrl +
                      "?response_type=code" +
                      "&client_id=" + _clientId +
                      "&resource=" + _powerBIApi +
                      "&redirect_uri=" + _redirectUrl;
            return Ok(url);
        }

        [HttpGet]
        [Route("directsignin")]
        public async Task<IHttpActionResult> GetTokenFromUserNamePassword(string userName, string password)
        {
            try
            {
                var paramStr =
                    "client_id=" + HttpUtility.UrlEncode(_clientId) +
                    "&client_secret=" + HttpUtility.UrlEncode(_clientSecrect) +
                    "&username=" + userName +
                    "&password=" + password +
                    "&grant_type=password" +
                    "&resource=https://analysis.windows.net/powerbi/api";

                //prepare request
                var request = new RestRequest(Method.POST);
                request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
                request.AddParameter("application/x-www-form-urlencoded", paramStr, ParameterType.RequestBody);

                //execute request
                var url = $"https://login.windows.net/{tenantId}/oauth2/token";
                var client = new RestClient(url);
                var response = client.Execute(request);
                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                    return Json(new ResponseWithToken()
                    {
                        Status = 0,
                        Message = response.StatusDescription
                    });

                //save db and return
                var resContent = JObject.Parse(response.Content);
                _settingService.SavePowerBIAccessToken(resContent["access_token"].ToString(), resContent["expires_on"].ToString(), resContent["refresh_token"].ToString());

                return await Task.FromResult(Json(new ResponseWithToken()
                {
                    Status = 1,
                    Message = "success",
                    AccessToken = resContent["access_token"].ToString()
                }));
            }
            catch (Exception ex)
            {
                return Json(new ResponseWithToken()
                {
                    Status = -1,
                    Message = ex.ToString()
                });
            }
        }

        [HttpGet]
        [Route("gettoken")]
        public async Task<IHttpActionResult> GetTokenFromCode(string code)
        {
            try
            {
                var paramStr =
                    "grant_type=authorization_code" +
                    "&client_id=" + HttpUtility.UrlEncode(_clientId) +
                    "&code=" + code +
                    "&redirect_uri=" + HttpUtility.UrlEncode(_redirectUrl) +
                    "&resource=https://analysis.windows.net/powerbi/api" +
                    "&client_secret=" + HttpUtility.UrlEncode(_clientSecrect);

                //prepare request
                var request = new RestRequest(Method.POST);
                request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
                request.AddParameter("application/x-www-form-urlencoded", paramStr, ParameterType.RequestBody);

                //execute request
                var url = $"https://login.windows.net/{tenantId}/oauth2/token";
                var client = new RestClient(url);
                var response = client.Execute(request);
                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                    return Json(new ResponseWithToken()
                    {
                        Status = 0,
                        Message = response.StatusDescription
                    });

                var resContent = JObject.Parse(response.Content);
                _settingService.SavePowerBIAccessToken(resContent["access_token"].ToString(), resContent["expires_on"].ToString(), resContent["refresh_token"].ToString());
                return await Task.FromResult(Json(new ResponseWithToken()
                {
                    Status = 1,
                    Message = "success",
                    AccessToken = resContent["access_token"].ToString()
                }));
            }
            catch (Exception ex)
            {
                return Json(new ResponseWithToken()
                {
                    Status = -1,
                    Message = ex.ToString()
                });
            }
        }

        [HttpGet]
        [Route("datasets")]
        public async Task<IHttpActionResult> GetListDataset()
        {
            var result = await _settingService.GetAccessToken();
            if (result.StartsWith("0_"))
                return Json(new ResponseWithToken()
                {
                    Status = -1,
                    Message = result.Substring(2)
                });
            try
            {
                var datasets = new Dictionary<string, string>();
                //string dsId = "", dsName = "";
                var json = await GetListDatasetService(result);
                if (await CheckResponseIsExpired(json))
                    return Json(new ResponseWithToken()
                    {
                        Status = -1,
                        Message = ""
                    });

                var lst = Enumerable.ToList<dynamic>(json["value"]) as List<dynamic>;
                lst.ForEach(x =>
                {
                    datasets.Add(x.id.Value, x.name.Value);
                    //var name = x.name.Value as string;
                    //if (name.StartsWith("NOIS_"))
                    //{
                    //    dsId = x.id.Value;
                    //    dsName = x.name.Value;
                    //}
                });

                return Json(new ResponseWithToken()
                {
                    Status = 1,
                    Message = string.Join("\n ", datasets.Select(x => x.Key + " : " + x.Value)),
                    //DsId = dsId,
                    //DsName = dsName,
                    //AccessToken = accessToken
                });
            }
            catch (Exception ex)
            {
                return Json(new ResponseWithToken()
                {
                    Status = 0,
                    Message = ex.ToString()
                });
            }
        }

        [HttpGet]
        [Route("createdataset")]
        public async Task<IHttpActionResult> CreateDataset()
        {
            var result = await _settingService.GetAccessToken();
            if (result.StartsWith("0_"))
                return Json(new ResponseWithToken()
                {
                    Status = -1,
                    Message = result.Substring(2)
                });

            try
            {
                var response = await CreateDatasetService(result);
                if (await CheckResponseIsExpired(response))
                    return Json(new ResponseWithToken()
                    {
                        Status = -1,
                        Message = ""
                    });

                return Json(new ResponseWithToken()
                {
                    Status = 1,
                    Message = $"Successfully create dataset, ID = {response["id"]}"
                });
            }
            catch (Exception ex)
            {
                return Json(new ResponseWithToken()
                {
                    Status = 0,
                    Message = ex.ToString()
                });
            }
        }

        [HttpGet]
        [Route("pushrow")]
        public async Task<IHttpActionResult> PushRows()
        {
            var accessTokenRes = await _settingService.GetAccessToken();
            if (accessTokenRes.StartsWith("0_"))
                return Json(new ResponseWithToken()
                {
                    Status = -1,
                    Message = accessTokenRes.Substring(2)
                });

            var json = await GetListDatasetService(accessTokenRes);
            if (await CheckResponseIsExpired(json))
                return Json(new ResponseWithToken()
                {
                    Status = -1,
                    Message = ""
                });

            var lst = Enumerable.ToList<dynamic>(json["value"]) as List<dynamic>;

            try
            {
                PushRowsService(lst.First().id.Value, accessTokenRes);
                return Json(new ResponseWithToken()
                {
                    Status = 1,
                    Message = $"Pushed successfully to dataset {lst.First().name.Value}"
                });
            }
            catch (Exception ex)
            {
                return Json(new ResponseWithToken()
                {
                    Status = 0,
                    Message = ex.ToString()
                });
            }
        }

        [HttpGet]
        [Route("populatedata")]
        public async Task<IHttpActionResult> PopulateData()
        {
            var accessTokenRes = await _settingService.GetAccessToken();
            if (accessTokenRes.StartsWith("0_"))
                return Json(new ResponseWithToken()
                {
                    Status = -1,
                    Message = accessTokenRes.Substring(2)
                });

            try
            {
                var createRes = await CreateDatasetService(accessTokenRes);
                if (await CheckResponseIsExpired(createRes))
                    return Json(new ResponseWithToken()
                    {
                        Status = -1,
                        Message = ""
                    });

                var pushRes = await PushRowsService(createRes["id"].ToString(), accessTokenRes);
                if (await CheckResponseIsExpired(createRes))
                    return Json(new ResponseWithToken()
                    {
                        Status = -1,
                        Message = ""
                    });

                return Json(new ResponseWithToken()
                {
                    Status = 1,
                    Message = $"Pushed successfully to dataset {createRes["name"]}"
                });
            }
            catch (Exception ex)
            {
                return Json(new ResponseWithToken()
                {
                    Status = 0,
                    Message = ex.ToString()
                });
            }
        }

        [HttpGet]
        [Route("resetdata")]
        public IHttpActionResult ResetData()
        {
            _settingService.ResetData();
            return Json(new ResponseWithToken()
            {
                Status = 1,
                Message = "Successfully reset data"
            });
        }
        #endregion
    }
}
