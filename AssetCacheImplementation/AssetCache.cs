using System;
using System.Collections.Generic;
using System.IO;

namespace AssetCacheImplementation
{
    public class AssetCache : IAssetCache
    {
        
        private Cache cache;

        private System.IO.StreamReader reader;
        private string lineStash;

        private string _pathToFile;
        private DateTime _fileLastTimeWrite;
        
        public bool IsBuiltAndReady { get; private set; }

        //Магические строки, нужные для обнаружения необходимых полей в файле
        private const string objectStartingMarker = "--- !u!";
        private const string fileIdMarker = "fileID:";
        private const string guidMarker = ", guid: ";
        private const string componentArrayMarker = "m_Component:";
        private const string componentEntryMarker = "component";

        
        private Dictionary<ulong, int> _assetFileIdToCount;
        private List<ulong> _gameObjects;
        private List<ulong> _otherEntities;

        public object Build(string path, Action interruptChecker)
        {
            var interruptCheckerInvokeCounter = 1000;
            if (File.GetLastWriteTime(path) != _fileLastTimeWrite || _pathToFile != path)
            {
                if (reader != null)
                {
                    reader.Close();
                }
                var baseStramer = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                lineStash = string.Empty;
                reader = new System.IO.StreamReader(baseStramer, true);
                _pathToFile = path;
                cache = new Cache(path);
                _fileLastTimeWrite = File.GetLastWriteTime(cache.PathToFile);
            }

            while (!reader.EndOfStream)
            {
                if (lineStash.Contains("--- !u!"))
                {
                    ParseObject(lineStash, out lineStash);
                }
                else
                {
                    lineStash = reader.ReadLine();
                }

                if (interruptCheckerInvokeCounter > 0)
                {
                    interruptCheckerInvokeCounter--;
                }
                else
                {
                    interruptChecker.Invoke();
                }
            }
            cache.FinishBuilding();
            reader.Close();
            return cache;
        }

        private void ParseObject(string firstString, out string lastReadString)
        {
            var objectFileId = GetObjectFileId(firstString);
            cache.ConstructSceneEntity(objectFileId);
            cache.SpecifySceneEntityType(objectFileId, reader.ReadLine());
            lastReadString = reader.ReadLine();
            while(!reader.EndOfStream && !IsLineStartsObject(lastReadString))
            {
                if (lastReadString.Contains(componentArrayMarker) && !lastReadString.Contains("[]"))
                {
                    lastReadString = reader.ReadLine();
                    while (lastReadString.Contains(componentEntryMarker))
                    {
                        cache.AddComponentToGameObject(objectFileId, GetReferenceFileId(lastReadString));
                        lastReadString = reader.ReadLine();
                    } 
                }

                if (lastReadString.Contains(fileIdMarker))
                {
                    if (lastReadString.Contains(guidMarker))
                    {
                        var assetPair = GetAssetFileIdAndGuid(lastReadString);
                        cache.ConstructAsset(assetPair.Item2, objectFileId, assetPair.Item1);
                    }
                    else
                    {
                        var reference = GetReferenceFileId(lastReadString);
                        if (reference != 0)
                        {
                            cache.AddLinkToSceneEntity(objectFileId, reference);
                        }
                    }
                }

                lastReadString = reader.ReadLine();
            }
        }

        private static ulong GetReferenceFileId(string line)
        {
            var startPosition = line.LastIndexOf(":") + 1;
            var endPosition = line.LastIndexOf("}");

            return ulong.Parse(line.Substring(startPosition, endPosition - startPosition));
        }

        private static bool IsLineStartsObject(string line)
        {
            return line.Contains(objectStartingMarker);
        }

        private static (ulong, string) GetAssetFileIdAndGuid(string line)
        {
            ulong fileId;
            string guid;
            var startPosition = line.LastIndexOf(fileIdMarker) + fileIdMarker.Length;
            var endPosition = line.LastIndexOf(guidMarker);

            fileId = ulong.Parse(line.Substring(startPosition, endPosition - startPosition));

            startPosition = line.LastIndexOf(guidMarker) + guidMarker.Length;
            endPosition = line.LastIndexOf(",");

            guid = line.Substring(startPosition, endPosition - startPosition);

            return (fileId, guid);
        }

        private static ulong GetObjectFileId(string line)
        {
            return ulong.Parse(line.Substring(line.IndexOf("&") + 1));
        }

        public void Merge(string path, object result)
        {
            if (!(result is Cache))
            {
                throw new ChachedFilePathException();
            }
            
            cache = (Cache)result;

            if (cache.PathToFile != path)
            {
                throw new CacheTypeMismatchException();
            }

            _gameObjects = new List<ulong>();
            _otherEntities = new List<ulong>();
            foreach(var fileId in cache.FileIds)
                    {
                        if (cache.IsGameObject(fileId))
                        {
                            _gameObjects.Add(fileId);
                        }
                        else
                        {
                            _otherEntities.Add(fileId);
                        }
                    }
                    
            ulong assetFileId;
            _assetFileIdToCount = new Dictionary<ulong, int>();
            foreach (var guid in cache.GuIds)
            {
                assetFileId = cache.GetAssetFileId(guid);
                if (_assetFileIdToCount.ContainsKey(assetFileId))
                {
                    _assetFileIdToCount[assetFileId] += cache.GetGuidUsage(guid);
                }
                else
                {
                    _assetFileIdToCount.Add(assetFileId, cache.GetGuidUsage(guid));
                }
            }

            IsBuiltAndReady = true;
        }

        public int GetLocalAnchorUsages(ulong anchor)
        {
            if (!IsBuiltAndReady)
            {
                throw new CacheIsInvalidException();
            }

            var usages = 0;
            if (_assetFileIdToCount.ContainsKey(anchor))
            {
                return _assetFileIdToCount[anchor];
            }
            if (_gameObjects.Contains(anchor))
            {
                usages = cache.FindUsages(anchor);
            }
            else
            {
                if (_otherEntities.Contains(anchor))
                {
                    usages = cache.FindUsages(anchor);
                    foreach(var fileID in cache.GetFileIdsReferencesBy(anchor))
                    {
                        if (cache.IsGameObject(fileID))
                        {
                            usages += (cache.HasGameObjectComponent(fileID, anchor)) ? 1 : 0;
                            break;
                        }
                    }
                }
            }
            return usages;
        }

        public int GetGuidUsages(string guid)
        {
            if (!IsBuiltAndReady)
            {
                throw new CacheIsInvalidException();
            }

            return cache.GetGuidUsage(guid);
        }

        public IEnumerable<ulong> GetComponentsFor(ulong gameObjectAnchor)
        {
            if (!IsBuiltAndReady)
            {
                throw new CacheIsInvalidException();
            }

            if (_gameObjects.Contains(gameObjectAnchor))
            {
                return cache.GetComponents(gameObjectAnchor);
            }
            return new ulong[0];
        } 

        ~AssetCache()
        {
            if (reader != null)
            {
                reader.Close();
            }
        }
    }
}