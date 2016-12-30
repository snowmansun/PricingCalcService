using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;

namespace PricingCalcService
{
    // 注意: 使用“重构”菜单上的“重命名”命令，可以同时更改代码和配置文件中的接口名“IPricingCalc”。
    [ServiceContract]
    public interface IPricingCalc
    {
        [OperationContract]
        void DoWork();




        [OperationContract]
        [WebInvoke(UriTemplate = "CalcPricing", Method = "POST", RequestFormat = WebMessageFormat.Json, ResponseFormat = WebMessageFormat.Json)]
        string CalcPricing(PricingCond pricingCond);
    }
}
