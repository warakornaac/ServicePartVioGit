using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServiceCatalog.Models
{
    public class StoreImportFittingUpload
    {
        public string Stkcode { get; set; }
        public string Axis { get; set; }
        public string Side { get; set; }
        public string Level { get; set; }
        public string Direction { get; set; }
        public string InsertedBy { get; set; }
        public string InsertedDate { get; set; }
        public string UpdatedBy { get; set; }
        public string UpdatedDate { get; set; }
        public string StatusImport { get; set; }
        public string ErrorImport { get; set; }
    }
}
