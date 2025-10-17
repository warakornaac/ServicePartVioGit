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
    public class SearchItemProduct : MsSQL
    {
        public SearchItemProduct() : base(Utils.GetConfig("ServiceCatalogDB"))
        {

        }
        public List<StoredSearchItemProductsModel> SearchItem(string Company, string SecCode, string StockGroup, string ProdCode, string Stkcode, string BrandId, string RowNumber, string ApiStatus, string CallDate)
        {
            var p = new SqlParameters();
            p.AddParams("@inCompany", Company);
            p.AddParams("@inSec", SecCode);
            p.AddParams("@inStockGroup", StockGroup);
            p.AddParams("@inProdCode", ProdCode);
            p.AddParams("@inStkcode", Stkcode);
            p.AddParams("@inBrandId", BrandId);
            p.AddParams("@inStatus", ApiStatus.ToTrim());
            p.AddParams("@inRowNumber", RowNumber);
            p.AddParams("@inDateCall", CallDate);

            var table = GetData(CmdStore("P_Search_Item_Product", p));
            return ConvertExtension.ConvertDataTable<StoredSearchItemProductsModel>(GetData(CmdStore("P_Search_Item_Product", p)));
        }
    }
}
