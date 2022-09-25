using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ApiClient
{
    public class RouteBuilder
    {
        public string[] Route { private set; get; }

        public RouteBuilder(params string[] route)
        {
            Route = route;
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();

            foreach (var s in Route)
                builder.Append('/').Append(s);

            return builder.ToString();
        }
    }
}
