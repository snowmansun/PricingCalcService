using PricingApi;
using PricingApiData;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.ServiceModel;
using System.ServiceModel.Activation;
using System.Text;

namespace PricingCalcService         
{
    // 注意: 使用“重构”菜单上的“重命名”命令，可以同时更改代码、svc 和配置文件中的类名“PricingCalc”。
    // 注意: 为了启动 WCF 测试客户端以测试此服务，请在解决方案资源管理器中选择 PricingCalc.svc 或 PricingCalc.svc.cs，然后开始调试。
    [AspNetCompatibilityRequirements(RequirementsMode = AspNetCompatibilityRequirementsMode.Allowed)]
    public class PricingCalc : IPricingCalc
    {
        public void DoWork()
        {
        }

        private Model model;
        private ModelController modelController;

        public string CalcPricing(PricingCond pricingCond)
        {
            // convert stream to string  
            //StreamReader reader = new StreamReader(ImageContext);
            //string text = reader.ReadToEnd();  


            string customerCode = pricingCond.customerCode;
            string date = pricingCond.calcDate;
            List<Products> productList = pricingCond.products;
            //string jsonProduct = "[{\"ProductId\":\"0000100005\",\"Quantity\":1.04167,\"UnitOfMeasure\":\"CS\"},{\"ProductId\":\"0000100001\",\"Quantity\":2.16667,\"UnitOfMeasure\":\"CS\"}]";

            //List<Products> productList = JSONStringToList<Products>(jsonProduct);
            //PricingCalcService.Products pdu = (PricingCalcService.Products)GetObjectByJson(jsonProduct, new PricingCalcService.Products());

            if (modelController == null)
            {
                modelController = new ModelController(
                   "PricingApiSql.SqlDbProvider, PricingApiSql",
                   "Data Source=115.112.171.100;Initial Catalog=PricingHCCB;Persist Security Info=True;User ID=sa;Password=flowers123*",
                   "yyyy-MM-dd");
                modelController.CustomerExtraFields = "CCC_DISC_IND,CCC_ROUNDRLS,ALAND/TAXK1";
                modelController.ProductExtraFields = "MTART,SCL_RET";

                model = null;
            }

            if (model == null)
            {
                model = new Model();
                modelController.LoadConfiguration(model);
            }

            modelController.LoadCustomerCache(model,
                 2022979,
                 DateTime.Today, Convert.ToDateTime("9999-01-01"));

            modelController.LoadCustomer(model,
            2022979,
            562937,
            DateTime.Today, Convert.ToDateTime("9999-01-01"));

            // Get Process
            PricingProcess process = model.Configuration.PricingProcesses[2];

            // Get Customer (Cache contains single customer)
            CustomerCache customerCache = model.CustomerCache[2022979];

            PricingDoc pricingDoc = new PricingDoc();
            pricingDoc.Customer = customerCache.Customers[562937];
            pricingDoc.PricingDate = Convert.ToDateTime(date);

            //json = @"ProductId":"0010651731","Quantity":1.04167,"UnitOfMeasure":"CS"},{"ProductId":"0010681531","Quantity":2.16667,"UnitOfMeasure":"CS"}";

            int i = 10;
            foreach (Products pdu in productList)
            {
                PricingItem pricingItem = new PricingItem(pricingDoc, 10);
                pricingItem.Product = pricingDoc.Customer.ProductsByRef[pdu.ProductId];
                pricingItem.Uom = model.Configuration.UnitOfMeasure[pdu.UnitOfMeasure];
                pricingItem.Quantity = pdu.Quantity;

                pricingDoc.PricingItems.Add(i, pricingItem);
                i += 10;
            }

            bool isAssort = modelController.GetAssortmentInfo(process, pricingDoc, model);

            // Execute Calculation
            process.ExecutePricing(pricingDoc, isAssort);

            string jsonResult = string.Empty;
            string jsonHead = "\"Summary\": {";
            string jsonDetail = "\"Products\":[";

            foreach (var item in pricingDoc.Outputs)
            {
                putJson(ref jsonHead, item.Value.PricingOutput.OutputName, item.Value.Amount != 0 ? item.Value.Amount.ToString("f" + item.Value.PricingOutput.Decimals) : item.Value.Amount.ToString());
            }
            decimal summary_freeGoodsQty = 0;

            foreach (var item in pricingDoc.PricingItems)
            {
                jsonDetail += "{";
                PricingItem pi = item.Value;
                putJson(ref jsonDetail, "Key", item.Key.ToString());
                putJson(ref jsonDetail, "ProductRef", pi.Product.ProductRef);
                putJson(ref jsonDetail, "ProductId", pi.Product.ProductId.ToString());
                putJson(ref jsonDetail, "ProductName", pi.Product.ProductDesc);
                putJson(ref jsonDetail, "Uom", pi.Uom.Uom);
                putJson(ref jsonDetail, "Quantity", pi.Quantity.ToString());
                putJson(ref jsonDetail, "TotalQuantity", pi.TotalQuantity.ToString());

                if (pi.ItemType == PricingItem.ITEM_TYPE_FREE_GOOD)
                {
                    putJson(ref jsonDetail, "IsFreeGoods", "true");
                }
                else
                {
                    putJson(ref jsonDetail, "IsFreeGoods", "false");
                }

                var listNormalMarkdtScopeCode = new Dictionary<string, string>();//本品
                var listFreeMarkdtScopeCode = new Dictionary<string, string>();//赠品

                string PriceListNo = string.Empty;
                string SchemaNo = string.Empty;

                foreach (var priceItem in pi.Outputs)
                {
                    putJson(ref jsonDetail, priceItem.Value.PricingOutput.OutputName, priceItem.Value.Amount != 0 ? priceItem.Value.Amount.ToString("f" + priceItem.Value.PricingOutput.Decimals) : "0");

                    //2.PriceListNo
                    if (!string.IsNullOrEmpty(priceItem.Value.MarketScopeCode) &&
                        priceItem.Value.MarketScopeCode.Equals("21"))
                    {
                        PriceListNo = priceItem.Value.RecordRef.ToString();
                    }
                }

                decimal freeGoodQty = pi.ExclFreeGoodQuantity + pi.InclFreeGoodQuantity;
                putJson(ref jsonDetail, "FreeGoodsQty", freeGoodQty.ToString());
                putJson(ref jsonDetail, "IsInclFreeGoods", pi.InclFreeGoodQuantity != 0 ? "true" : "false");
                jsonDetail = jsonDetail.TrimEnd(',');

                summary_freeGoodsQty += freeGoodQty;
                jsonDetail += "},";
            }
            putJson(ref jsonHead, "SummaryFreeGoodsQty", summary_freeGoodsQty.ToString());

            jsonHead = jsonHead.TrimEnd(',') + "},";
            jsonDetail = jsonDetail.TrimEnd(',') + "]";
            jsonResult = "{" + jsonHead + jsonDetail + "}";

            return jsonResult;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="JsonScr"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        private void putJson(ref string JsonScr, string key, string value)
        {
            JsonScr += "\"" + key + "\":\"" + value + "\",";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="JsonStr"></param>
        /// <returns></returns>
        public static List<Products> JSONStringToList<T>(string JsonStr)
        {

            List<Products> _Test = new List<Products>();
            DataContractJsonSerializer _Json = new DataContractJsonSerializer(_Test.GetType());
            byte[] _Using = System.Text.Encoding.UTF8.GetBytes(JsonStr);
            System.IO.MemoryStream _MemoryStream = new System.IO.MemoryStream(_Using);
            _MemoryStream.Position = 0;
            _Test = (List<Products>)_Json.ReadObject(_MemoryStream);

            return _Test;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="jsonString"></param>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static Object GetObjectByJson(string jsonString, Object obj)
        {
            //实例化DataContractJsonSerializer对象，需要待序列化的对象类型
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(obj.GetType());
            //把Json传入内存流中保存
            MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonString));
            // 使用ReadObject方法反序列化成对象
            return serializer.ReadObject(stream);
        }

        /// <summary>
        /// 读取文件流
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public byte[] ReadToEnd(System.IO.Stream stream)
        {
            long originalPosition = 0;

            if (stream.CanSeek)
            {
                originalPosition = stream.Position;
                stream.Position = 0;
            }

            try
            {
                byte[] readBuffer = new byte[4096];

                int totalBytesRead = 0;
                int bytesRead;

                while ((bytesRead = stream.Read(readBuffer, totalBytesRead, readBuffer.Length - totalBytesRead)) > 0)
                {
                    totalBytesRead += bytesRead;

                    if (totalBytesRead == readBuffer.Length)
                    {
                        int nextByte = stream.ReadByte();
                        if (nextByte != -1)
                        {
                            byte[] temp = new byte[readBuffer.Length * 2];
                            Buffer.BlockCopy(readBuffer, 0, temp, 0, readBuffer.Length);
                            Buffer.SetByte(temp, totalBytesRead, (byte)nextByte);
                            readBuffer = temp;
                            totalBytesRead++;
                        }
                    }
                }

                byte[] buffer = readBuffer;
                if (readBuffer.Length != totalBytesRead)
                {
                    buffer = new byte[totalBytesRead];
                    Buffer.BlockCopy(readBuffer, 0, buffer, 0, totalBytesRead);
                }
                return buffer;
            }
            finally
            {
                if (stream.CanSeek)
                {
                    stream.Position = originalPosition;
                }
            }
        }
    }
}
