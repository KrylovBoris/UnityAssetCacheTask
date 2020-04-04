using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssetCacheImplementation
{

    public class OperationCanceledException : System.Exception{};

    internal class ChachedFilePathException : System.Exception{};

    internal class CacheTypeMismatchException : System.Exception { };

    internal class CacheIsInvalidException : System.Exception { };
}
