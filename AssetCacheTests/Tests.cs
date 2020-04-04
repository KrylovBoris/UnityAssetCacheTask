using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using AssetCacheImplementation;
using System.Collections.Generic;

namespace AssetCacheTests
{
    [TestFixture]
    public class Tests
    {
        private Dictionary<string, AssetCache> caches;
        private ulong[][] componentsTestSets;

        [OneTimeSetUp]
        public void Init()
        {
            caches = new Dictionary<string, AssetCache>();

            var cache = new AssetCache();
            var path = PathToFile("SampleScene.unity");
            object test = cache.Build(path, () => { });
            cache.Merge(path, test);

            caches.Add("SampleScene.unity", cache);

            cache = new AssetCache();
            path = PathToFile("SimpleScene.unity");
            test = cache.Build(path, () => { });
            cache.Merge(path, test);
            caches.Add("SimpleScene.unity", cache);

            componentsTestSets = new ulong[][]
            {
                new ulong[] { 757051390, 757051389, 757051388, 757051387, 757051386 },
                new ulong[] { },
                new ulong[] { 17641, 17644, 17643, 17642 },
                new ulong[] { 1278330992, 1278330996, 1278330995, 1278330994, 1278330993 },
                new ulong[] { 705507995, 705507994 }
            };
        }

        private string PathToFile(string localPath)
        {
            return Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), localPath);
        }

        [Test]
        public void OperationCancelTest()
        {
            var cache = new AssetCache();
            var path = PathToFile("SampleScene.unity");
            object test;
            try
            {
                cache.Build(path, () => { throw new AssetCacheImplementation.OperationCanceledException(); });
            }
            catch { }
            test = cache.Build(path, () => { });

            cache.Merge(path, test);

            Assert.IsTrue(cache.IsBuiltAndReady);
        }

        [TestCase("SampleScene.unity")]
        [TestCase("SimpleScene.unity")]
        public void BuildTest(string fileName)
        {
            var cache = new AssetCache();
            var path = PathToFile(fileName);
            object test = cache.Build(path, () => { });
            cache.Merge(path, test);
            Assert.IsTrue(cache.IsBuiltAndReady);
        }

        [TestCase("SimpleScene.unity", "0000000000000000e000000000000000", 4)]
        [TestCase("SimpleScene.unity", "801a0a604e828724da83b96f51cee06d", 2)]
        [TestCase("SampleScene.unity", "9eaebf3930936434da023d89df8a186c", 48286)]
        [TestCase("SampleScene.unity", "385dae4ffcaf7134db7c8d7d0cc5fcc9", 96786)]
        [TestCase("SampleScene.unity", "801a0a604e828724da83b96f51cee06d", 0)]
        public void GuidCountTest(string fileName, string guid, int guidTrueCount)
        {
            var cache = caches[fileName];
            Assert.AreEqual(cache.GetGuidUsages(guid), guidTrueCount);
        }

        [TestCase("SimpleScene.unity", (ulong)757051385, 0)]
        [TestCase("SampleScene.unity", (ulong)757051385, 1)]
        [TestCase("SampleScene.unity", (ulong)17640, 2)]
        [TestCase("SampleScene.unity", (ulong)1278330991, 3)]
        [TestCase("SimpleScene.unity", (ulong)705507993, 4)]
        public void GetComponentsTest(string fileName, ulong anchor, int componentTestSet)
        {
            var cache = caches[fileName];
            Assert.AreEqual(cache.GetComponentsFor(anchor), componentsTestSets[componentTestSet]);
        }

        [TestCase("SimpleScene.unity", (ulong)157, 0)]
        [TestCase("SimpleScene.unity", (ulong)150798671, 6)]
        [TestCase("SampleScene.unity", (ulong)11500000, 241557)]
        [TestCase("SampleScene.unity", (ulong)1278330992, 3)]
        [TestCase("SimpleScene.unity", (ulong)2100000, 3)]
        [TestCase("SimpleScene.unity", (ulong)2, 0)]
        [TestCase("SimpleScene.unity", (ulong)150798673, 1)]
        [TestCase("SimpleScene.unity", (ulong)705507994, 2)]
        [TestCase("SampleScene.unity", (ulong)705507994, 0)]
        [TestCase("SampleScene.unity", (ulong)1443645507, 6)]
        [TestCase("SimpleScene.unity", (ulong)2100000, 3)]
        public void GetAnchorCountTest(string fileName, ulong anchor, int trueCount)
        {
            var cache = caches[fileName];
            Assert.AreEqual(cache.GetLocalAnchorUsages(anchor), trueCount);
        }

        [Test]
        public void GetAnchorUsagesExceptionTest()
        {
            var cache = new AssetCache();
            var path = PathToFile("SimpleScene.unity");
            ulong anchor = 1278330992;
            object test = cache.Build(path, () => { });
            //cache.Merge(path, test);
            Assert.Catch(() => { cache.GetLocalAnchorUsages(anchor); });
        }

        [Test]
        public void GetComponentsExceptionTest()
        {
            var cache = new AssetCache();
            var path = PathToFile("SimpleScene.unity");
            ulong anchor = 1278330992;
            object test = cache.Build(path, () => { });
            //cache.Merge(path, test);
            Assert.Catch(() => { cache.GetComponentsFor(anchor); });
        }

        [Test]
        public void GuidCountExceptionTest()
        {
            var cache = new AssetCache();
            var path = PathToFile("SimpleScene.unity");
            var anchor = "801a0a604e828724da83b96f51cee06d";
            object test = cache.Build(path, () => { });
            //cache.Merge(path, test);
            Assert.Catch(() => { cache.GetGuidUsages(anchor); });
        }

        [Test]
        public void DoubleMergeTest()
        {
            var cache = new AssetCache();
            var path1 = PathToFile("SimpleScene.unity");
            var path2 = PathToFile("SampleScene.unity");
            object test1 = cache.Build(path1, () => { });
            object test2 = cache.Build(path2, () => { });

            cache.Merge(path1, test1);
            cache.Merge(path2, test2);

            Assert.IsTrue(cache.IsBuiltAndReady);

        }
    }

}