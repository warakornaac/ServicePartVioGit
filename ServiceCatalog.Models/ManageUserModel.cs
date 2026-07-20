using System;
using System.ComponentModel.DataAnnotations;

namespace ServiceCatalog.Models
{
    public class UsrGrp
    {
        [Key]
        public int ID { get; set; }
        public string UserID { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public int UserType { get; set; }
        public string Company { get; set; }
        public string Department { get; set; }
        public string Email { get; set; }
        public string Slmcod { get; set; }
        public DateTime InsertedDate { get; set; }
        public string InsertedBy { get; set; }
        public DateTime UpdatedDate { get; set; }
        public string UpdatedBy { get; set; }
    }
}
