using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServiceCatalog.Models
{
    public class VIO_BeforeAfter
    {
        public string KType { get; set; }
        public string MarketSegment { get; set; }
        public string VehicleSegment { get; set; }
        public string Maker { get; set; }
        public string Model { get; set; }
        public string Body { get; set; }
        public string Engine { get; set; }
        public string DriveType { get; set; }
        public string YearFrom { get; set; }
        public string TruType { get; set; }
    }
    public class VIO_Approave
    {
        public string MarketSegment { get; set; }
        public string VehicleSegment { get; set; }
        public string Maker { get; set; }
        public string Model { get; set; }
        public string Body { get; set; }
        public string BodyType { get; set; }
        public string EngineType { get; set; }
        public string Strokes { get; set; }
        public string Old_Ktype { get; set; }
        public string New_Ktype { get; set; }
        public string Old_DriveType { get; set; }
        public string New_DriveType { get; set; }
        public string Old_YearFrom { get; set; }
        public string New_YearFrom { get; set; }
        public string Old_YearTo { get; set; }
        public string New_YearTo { get; set; }
        public string Old_TruType { get; set; }
        public string New_TruType { get; set; }
    }
    public class ListLinkage
    {
        public string Company { get; set; }
        public string STKCOD { get; set; }
        public string STKDES { get; set; }
        public string KType { get; set; }
        public string TruType { get; set; }

    }


    public class LinkageApproveChangeViewModel
    {
        public VIO_Approave apprvVIO { get; set; }
        public List<ListLinkage> itemLink { get; set; }
    }
}
