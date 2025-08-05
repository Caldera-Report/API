using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace API.Models.Responses
{
    public class SearchResponse
    {
        public required List<SearchResult> Results { get; set; }
    }
    public class SearchResult
    {
        public required string DisplayName { get; set; }
        public required int DisplayNameCode { get; set; }
        public required string DestinyMembershipId { get; set; }
        public required int MembershipType { get; set; }
    }
}
