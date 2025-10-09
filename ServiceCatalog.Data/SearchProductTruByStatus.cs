using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using My.Data;
using ServiceCatalog.Models;
using ServiceCatalog.Library;

namespace ServiceCatalog.Data
{
    public class SearchProductTruByStatus : MsSQL
    {
        public SearchProductTruByStatus() : base(Utils.GetConfig("ServiceCatalogDB"))
        {

        }
        public List<StoredSearchProductTruByStatusModel> SearchProductTru(string Stkcode, string BrandId, string RowNumber, string ApiStatus)
        {
            var p = new SqlParameters();
            p.AddParams("@inStkcode", Stkcode);
            p.AddParams("@inBrandId", BrandId);
            p.AddParams("@inStatus", ApiStatus.ToTrim());
            p.AddParams("@inRowNumber", RowNumber);

            var table = GetData(CmdStore("P_Search_Product_Tru_By_Status", p));
            return ConvertExtension.ConvertDataTable<StoredSearchProductTruByStatusModel>(GetData(CmdStore("P_Search_Product_Tru_By_Status", p)));
        }
    }
}
