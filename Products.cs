using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Web;

namespace PricingCalcService
{
    [DataContract]
    public class Products
    {
        [DataMember]
        public string ProductId { get; set; }

        [DataMember]
        public decimal Quantity { get; set; }

        [DataMember]
        public string UnitOfMeasure { get; set; }

    }
}