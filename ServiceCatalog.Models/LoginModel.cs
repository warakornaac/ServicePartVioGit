using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

namespace ServiceCatalog.Models
{
    public class LoginModel
    {
        [Required]
        //[EmailAddress]
        [StringLength(150)]
        [Display(Name = "User: ")]
        public string Usre { get; set; }
        [Required]
        [DataType(DataType.Password)]
        [StringLength(150, MinimumLength = 2)]
        [Display(Name = "Password: ")]
        public string Password { get; set; }
    }
}
