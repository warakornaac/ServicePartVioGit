using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServiceCatalog.Models
{
    public class PrepareProductApiModel
    {
        public string Stkcode { get; set; }
        public int BrandId { get; set; }
        public string BrandName { get; set; }
        public string ProductDescriptionCount { get; set; }
        public DateTime ProductDescriptionDate { get; set; }
        public string ProductSpecCount { get; set; }
        public DateTime ProductSpecDate { get; set; }
        public string ProductOemCount { get; set; }
        public DateTime ProductOemDate { get; set; }
        public string ProductCompetitorCount { get; set; }
        public DateTime ProductCompetitorDate { get; set; }
        public string ProductImageCount { get; set; }
        public DateTime ProductImageDate { get; set; }
        public string ProductLinkageCount { get; set; }
        public DateTime ProductLinkageDate { get; set; }
        public string ApiStatus { get; set; }
        public string ApiStatusRemark { get; set; }
        public DateTime ApiStartDate { get; set; }
        public DateTime ApiEndDate { get; set; }
        public string InsertedBy { get; set; }
        public DateTime InsertedDate { get; set; }
        public string UpdatedBy { get; set; }
        public DateTime UpdatedDate { get; set; }
    }
}
