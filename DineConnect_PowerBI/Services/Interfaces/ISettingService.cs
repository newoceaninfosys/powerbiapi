using System.Threading.Tasks;

namespace DineConnect_PowerBI.Services.Interfaces
{
    public interface ISettingService
    {
        void SavePowerBIAccessToken(string accessToken, string expiredOn, string refreshToken);
        Task<string> GetAccessToken();
        void ResetData();
    }
}
