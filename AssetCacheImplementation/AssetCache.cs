using System;
using System.Collections.Generic;
using System.IO;

namespace AssetCacheImplementation
{
    public class AssetCache : IAssetCache
    {
        //Структура, которую строит класс. Служит для обеспечения инкрементальности
        private Cache cache;

        //Число объектов, которые нужно прочитать из файла прежде чем произойдёт проверка на прерывание
        private readonly int _objectsToReadUntilInterrupt;

        //Поток для чтения из файла.
        private System.IO.StreamReader reader;

        //Последняя прочитанная строка. Нужна для поддержки инкрементальности 
        private string lineStash;
        //Путь к кэшируемому файлу и timestamp последнего изменения этого файла. Необходимы для проверки того, что
        private string _pathToFile;
        private DateTime _fileLastTimeWrite;

        //Флаг того, что кэш готов к использованию (были вызваны Build и Merge)
        public bool IsBuiltAndReady { get; private set; }

        //Магические строки, нужные для обнаружения необходимых полей в файле
        private const string objectStartingMarker = "--- !u!";
        private const string fileIdMarker = "fileID:";
        private const string guidMarker = ", guid: ";
        private const string componentArrayMarker = "m_Component:";
        private const string componentEntryMarker = "component";

        //Информация извлечённая из кэша при помощи метода Merge
        private Dictionary<ulong, int> _assetFileIdToCount;
        private List<ulong> _gameObjects;
        private List<ulong> _otherEntities;

        #region Build cache
        public object Build(string path, Action interruptChecker)
        {
            
            var interruptCheckerInvokeCounter = _objectsToReadUntilInterrupt;
            
            //Если файл изменился или на кэширование был подан новый файл, перезапустить поток чтения и подготовить структуру для записи
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
                if (IsLineStartsObject(lineStash))
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
                    //Вызов проверки прерывания. Если interruptChecker вызовет исключение, поток чтения останется открыт, и при следующем вызове Build, можно продолжить чтение из файла
                    interruptChecker.Invoke();
                }
            }
            
            cache.FinishBuilding();
            reader.Close();
            return cache;
        }

        /// <summary>
        /// Читает из файла объект сцены, извлекает из него ссылки на другие объекты
        /// </summary>
        /// <param name="firstString"> Первая строка, которую обрабатывает поток.</param>
        /// <param name="lastReadString"> Последняя строка, на которой остановился поток.</param>
        private void ParseObject(string firstString, out string lastReadString)
        {
            var objectFileId = GetObjectFileId(firstString);
            cache.ConstructSceneEntity(objectFileId);
            cache.SpecifySceneEntityType(objectFileId, reader.ReadLine());
            lastReadString = reader.ReadLine();
            while(!reader.EndOfStream && !IsLineStartsObject(lastReadString))
            {
                // Блок поиска ссылок на компоненты
                if (lastReadString.Contains(componentArrayMarker) && !lastReadString.Contains("[]"))
                {
                    lastReadString = reader.ReadLine();
                    //Чтение ссылок на компоненты
                    while (lastReadString.Contains(componentEntryMarker))
                    {
                        cache.AddComponentToGameObject(objectFileId, GetReferenceFileId(lastReadString));
                        lastReadString = reader.ReadLine();
                    } 
                }

                //Блок поиска ссылок на объекты
                if (lastReadString.Contains(fileIdMarker))
                {
                    if (lastReadString.Contains(guidMarker))
                    {
                        //Найдена ссылка на ассет
                        var assetPair = GetAssetFileIdAndGuid(lastReadString);
                        cache.ConstructAsset(assetPair.Item2, objectFileId, assetPair.Item1);
                    }
                    else
                    {
                        //Найдена обычная ссылка на объект
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

        //Извлечение идентификатора на объект из ссылки
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

        //Извлечение типа ассета и GUID
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

        //Извлечение идентификатора на объект из объявления
        private static ulong GetObjectFileId(string line)
        {
            return ulong.Parse(line.Substring(line.IndexOf("&") + 1));
        }

        #endregion

        public void Merge(string path, object result)
        {
            //Проверка на то, что в метод передан объект подходящего класса
            if (!(result is Cache))
            {
                throw new ChachedFilePathException();
            }
            
            //Сохранение кэша внутри класса
            cache = (Cache)result;

            //Проверка на то, что переданный файл совпадает с тем, что был закэширован в result
            if (cache.PathToFile != path)
            {
                throw new CacheTypeMismatchException();
            }

            //Классификация закэшированных объектов и запись их fileID в списки
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
            
            //Подсчёт использований fileID для ассетов. 
            //Так как fileID для ассетов возвращает тип ассета, к которому относится ассет, этот участок кода вычисляет, сколько раз был использован ассет заданного каждого типа.
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

            //Кэш готов к использованию
            IsBuiltAndReady = true;
        }

        public int GetLocalAnchorUsages(ulong anchor)
        {
            if (!IsBuiltAndReady)
            {
                throw new CacheIsInvalidException();
            }

            
            //anchor — fileID ассета
            if (_assetFileIdToCount.ContainsKey(anchor))
            {
                return _assetFileIdToCount[anchor];
            }

            //anchor — fileID объекта на сцене.
            var anchorIsGameObject = _gameObjects.Contains(anchor);
            var usages = 0;

            if (anchorIsGameObject || _otherEntities.Contains(anchor))
            {
                usages = cache.FindUsages(anchor);
            }

            if (!anchorIsGameObject)
            {
                //Если anchor принадлежит не gameObject, проверить, не было ли на него ссылок в поле m_Component
                foreach (var fileID in cache.GetFileIdsReferencesBy(anchor))
                {
                    if (cache.IsGameObject(fileID))
                    {
                        usages += (cache.HasGameObjectComponent(fileID, anchor)) ? 1 : 0;
                        break;
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

        public AssetCache(int interruptCheckerThres = 1000)
        {
            _objectsToReadUntilInterrupt = interruptCheckerThres;
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