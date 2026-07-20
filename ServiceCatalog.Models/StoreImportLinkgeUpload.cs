using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServiceCatalog.Models
{
    public class StoreImportLinkgeUpload
    {
        public string Stkcode { get; set; }
        public int SeqLinkage { get; set; }
        public string KType { get; set; }
        public string TruType { get; set; }
        public string InsertedBy { get; set; }
        public string InsertedDate { get; set; }
        public string UpdatedBy { get; set; }
        public string UpdatedDate { get; set; }
        public string StatusImport { get; set; }
        public string ErrorImport { get; set; }
    }
}
