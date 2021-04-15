using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
/**
 * there is any security implementation 
 * maybe in the future
 * that is just an example of how to use microservices
 */
namespace MicroRabbit.Banking.Domain.Models
{
    public class Account
    {
        public int Id { get; set; }
        public string AccountType { get; set; }

        public decimal AccountBalance { get; set; }
    }
}
