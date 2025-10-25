using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bardcoded.Data
{
    public record Health(bool IsUp)
    {
        public static readonly Health Down = new Health(false);
        public static readonly Health Up = new Health(true);
    }
}
