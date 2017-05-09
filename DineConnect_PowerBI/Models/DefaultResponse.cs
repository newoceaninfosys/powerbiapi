using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DineConnect_PowerBI.Models
{
    public class DefaultResponse
    {
        public string Message { get; set; }
        public int Status { get; set; }
    }

    public class ResponseWithToken : DefaultResponse
    {
        public string AccessToken { get; set; }
    }
}