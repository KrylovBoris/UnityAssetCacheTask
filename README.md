# UnityAssetCacheTask
Задача 2 тестового задания для стажировки на проекте GAMEDEV: Productivity tools for Unity Editor компании JetBrains.

Решение состоит из двух проектов:

* [Реализация](https://github.com/KrylovBoris/UnityAssetCacheTask/tree/master/AssetCacheImplementation)
* [Юнит-тесты](https://github.com/KrylovBoris/UnityAssetCacheTask/tree/master/AssetCacheTests)

## Реализация
Реализация интерфейса, данного в условии, размещена в файле [AssetCache.cs](https://github.com/KrylovBoris/UnityAssetCacheTask/blob/master/AssetCacheImplementation/AssetCache.cs). Файл [Cache.cs](https://github.com/KrylovBoris/UnityAssetCacheTask/blob/master/AssetCacheImplementation/Cache.cs) содержит структуру-контейнер, в которую кэшируется информация, требуемая по заданию. Файл [CacheBuildingExceptions.cs](https://github.com/KrylovBoris/UnityAssetCacheTask/blob/master/AssetCacheImplementation/CacheBuildingExceptions.cs) содержит исключения, которые выбрасывают функции при некорректном их вызове.

## Юнит-тесты
27 юнит-тестов были реализованы с помощью фреймворка NUnit.

## Диаграмма классов
[UML диаграмма классов решения](https://github.com/KrylovBoris/UnityAssetCacheTask/blob/master/UML/ClassUML.jpg)

