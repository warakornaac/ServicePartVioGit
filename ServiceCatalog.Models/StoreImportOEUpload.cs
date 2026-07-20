using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServiceCatalog.Models
{
    public class StoreImportOEUpload
    {
        public string Stkcode { get; set; }
        public int SeqOem { get; set; }
        public string OemNumber { get; set; }
        public string MakerName { get; set; }
        public string InsertedBy { get; set; }
        public string InsertedDate { get; set; }
        public string UpdatedBy { get; set; }
        public string UpdatedDate { get; set; }
        public string StatusImport { get; set; }
        public string ErrorImport { get; set; }
    }
}
