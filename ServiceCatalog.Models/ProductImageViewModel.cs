using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServiceCatalog.Models
{
    public class ProductImageViewModel
    {
        public string Company { get; set; }
        public string Stkcode { get; set; }
        public string Stkdesc { get; set; }
        public string BrandId { get; set; }
        public string BrandName { get; set; }
        public string Category { get; set; }
        public string ProductCode { get; set; }
        public string ProductName { get; set; }
        public int SeqImage { get; set; }
        public string Filename { get; set; }
        public string Url { get; set; }
    }
}