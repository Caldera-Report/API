using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace API.Models.DB
{
    public class Player
    {
        public long Id { get; set; }
        public string DisplayName { get; set; }
        public int MembershipType { get; set; }

    }
}
