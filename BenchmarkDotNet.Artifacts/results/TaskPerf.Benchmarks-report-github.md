``` ini

BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19042
Intel Xeon CPU E5-1620 0 3.60GHz, 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=5.0.104
  [Host]     : .NET Core 5.0.6 (CoreCLR 5.0.621.22011, CoreFX 5.0.621.22011), X64 RyuJIT DEBUG
  DefaultJob : .NET Core 5.0.6 (CoreCLR 5.0.621.22011, CoreFX 5.0.621.22011), X64 RyuJIT


```
|                                      Method |             Categories |           Mean |         Error |        StdDev |         Median |  Ratio | RatioSD |      Gen 0 |   Gen 1 | Gen 2 |   Allocated |
|-------------------------------------------- |----------------------- |---------------:|--------------:|--------------:|---------------:|-------:|--------:|-----------:|--------:|------:|------------:|
|                   ManyWriteFile_CSharpTasks |          ManyWriteFile |     4,186.6 μs |      81.32 μs |     201.00 μs |     4,187.4 μs |   1.00 |    0.00 |    15.6250 |       - |     - |    117288 B |
|                   ManyWriteFile_taskBuilder |          ManyWriteFile |     5,744.5 μs |     124.34 μs |     356.76 μs |     5,712.3 μs |   1.38 |    0.11 |    62.5000 |       - |     - |    444882 B |
|                         ManyWriteFile_async |          ManyWriteFile |     6,108.8 μs |     121.27 μs |     271.23 μs |     6,132.1 μs |   1.46 |    0.10 |   132.8125 |       - |     - |    704991 B |
|                          ManyWriteFile_task |          ManyWriteFile |     5,014.3 μs |     100.18 μs |     204.65 μs |     4,973.0 μs |   1.19 |    0.07 |    15.6250 |       - |     - |    116996 B |
|                        ManyWriteFile_async2 |          ManyWriteFile |     5,358.4 μs |     106.05 μs |     211.79 μs |     5,374.1 μs |   1.27 |    0.07 |    15.6250 |       - |     - |    117140 B |
|                                             |                        |                |               |               |                |        |         |            |         |       |             |
|                   NonAsyncBinds_CSharpTasks |          NonAsyncBinds |    17,444.8 μs |     374.32 μs |   1,097.80 μs |    17,030.4 μs |   1.00 |    0.00 | 15125.0000 |       - |     - |  79200000 B |
|                   NonAsyncBinds_taskBuilder |          NonAsyncBinds |    26,000.1 μs |     440.68 μs |     880.09 μs |    25,621.3 μs |   1.47 |    0.09 | 22187.5000 |       - |     - | 116000000 B |
|                         NonAsyncBinds_async |          NonAsyncBinds | 1,303,920.2 μs |  63,453.67 μs | 177,930.90 μs | 1,269,891.9 μs |  74.52 |   10.59 | 52000.0000 |       - |     - | 276000000 B |
|                        NonAsyncBinds_async2 |          NonAsyncBinds |    24,213.0 μs |     191.23 μs |     178.88 μs |    24,168.6 μs |   1.38 |    0.09 | 18812.5000 | 62.5000 |     - |  98400000 B |
|                          NonAsyncBinds_task |          NonAsyncBinds |    16,693.5 μs |     302.84 μs |     759.75 μs |    16,586.1 μs |   0.95 |    0.07 | 15125.0000 |       - |     - |  79200000 B |
|                                             |                        |                |               |               |                |        |         |            |         |       |             |
|                      AsyncBinds_CSharpTasks |             AsyncBinds |     8,335.4 μs |     190.47 μs |     558.61 μs |     8,197.5 μs |   1.00 |    0.00 |    15.6250 |       - |     - |    112119 B |
|                      AsyncBinds_taskBuilder |             AsyncBinds |    11,567.6 μs |     229.28 μs |     522.18 μs |    11,360.0 μs |   1.39 |    0.12 |   296.8750 |       - |     - |   1559252 B |
|                            AsyncBinds_async |             AsyncBinds |   127,872.2 μs |   3,090.27 μs |   8,866.56 μs |   127,900.8 μs |  15.38 |    1.47 |  1333.3333 |       - |     - |   8312000 B |
|                             AsyncBinds_task |             AsyncBinds |     9,897.9 μs |     314.55 μs |     927.46 μs |    10,058.4 μs |   1.19 |    0.15 |    31.2500 |       - |     - |    192096 B |
|                           AsyncBinds_async2 |             AsyncBinds |     8,165.2 μs |     156.64 μs |     347.09 μs |     8,051.9 μs |   0.98 |    0.07 |    62.5000 |       - |     - |    352218 B |
|                                             |                        |                |               |               |                |        |         |            |         |       |             |
|                  SingleSyncTask_CSharpTasks |         SingleSyncTask |     8,668.5 μs |     170.65 μs |     233.59 μs |     8,595.4 μs |   1.00 |    0.00 |          - |       - |     - |           - |
|                  SingleSyncTask_taskBuilder |         SingleSyncTask |    12,402.4 μs |     103.89 μs |      92.10 μs |    12,366.7 μs |   1.43 |    0.04 |  9171.8750 |       - |     - |  48000000 B |
|                        SingleSyncTask_async |         SingleSyncTask | 3,659,569.1 μs | 109,062.88 μs | 298,557.86 μs | 3,576,228.4 μs | 409.11 |   38.68 | 91000.0000 |       - |     - | 475999216 B |
|                         SingleSyncTask_task |         SingleSyncTask |    10,642.1 μs |      90.05 μs |      84.23 μs |    10,622.2 μs |   1.23 |    0.04 |          - |       - |     - |           - |
|                       SingleSyncTask_async2 |         SingleSyncTask |    28,177.5 μs |     263.90 μs |     220.37 μs |    28,134.6 μs |   3.25 |    0.08 |  7625.0000 |       - |     - |  40000000 B |
|                                             |                        |                |               |               |                |        |         |            |         |       |             |
|                  SyncBuilderLoop_NormalCode |                   sync |    81,206.3 μs |   1,598.95 μs |   1,570.38 μs |    80,813.7 μs |   1.00 |    0.00 | 36714.2857 |       - |     - | 192176000 B |
|                SyncBuilderLoop_WorkflowCode |                   sync |    81,811.3 μs |   1,610.38 μs |   3,140.93 μs |    80,621.9 μs |   1.02 |    0.05 | 36714.2857 |       - |     - | 192176000 B |
|                                             |                        |                |               |               |                |        |         |            |         |       |             |
|               TinyVariableSizedList_Builtin |  TinyVariableSizedList |    57,762.3 μs |   1,721.40 μs |   4,827.00 μs |    56,313.5 μs |   1.00 |    0.00 | 20375.0000 |       - |     - | 106666656 B |
|            TinyVariableSizedList_NewBuilder |  TinyVariableSizedList |    17,122.1 μs |     341.76 μs |     650.23 μs |    17,233.0 μs |   0.29 |    0.03 |  2031.2500 |       - |     - |  10666656 B |
|                                             |                        |                |               |               |                |        |         |            |         |       |             |
|                   VariableSizedList_Builtin |      VariableSizedList |   330,273.8 μs |   6,534.98 μs |  13,051.05 μs |   328,659.3 μs |   1.00 |    0.00 | 63000.0000 |       - |     - | 330666624 B |
|                VariableSizedList_NewBuilder |      VariableSizedList |   167,451.6 μs |   2,840.89 μs |   2,217.98 μs |   167,326.7 μs |   0.51 |    0.02 | 44750.0000 |       - |     - | 234666934 B |
|                                             |                        |                |               |               |                |        |         |            |         |       |             |
|                       FixedSizeList_Builtin |         FixedSizedList |   100,128.5 μs |   1,968.29 μs |   3,885.21 μs |   100,084.8 μs |   1.00 |    0.00 | 61166.6667 |       - |     - | 320000000 B |
|                    FixedSizeList_NewBuilder |         FixedSizedList |   229,639.0 μs |   4,589.37 μs |  11,846.63 μs |   227,278.1 μs |   2.30 |    0.13 | 61000.0000 |       - |     - | 320000000 B |
|                                             |                        |                |               |               |                |        |         |            |         |       |             |
|              TinyVariableSizedArray_Builtin | TinyVariableSizedArray |   100,414.3 μs |   1,995.07 μs |   4,462.26 μs |   100,631.3 μs |   1.00 |    0.00 | 30000.0000 |       - |     - | 157333304 B |
|           TinyVariableSizedArray_NewBuilder | TinyVariableSizedArray |    28,538.5 μs |     632.86 μs |   1,825.93 μs |    28,426.7 μs |   0.29 |    0.02 | 10687.5000 |       - |     - |  55999968 B |
|                                             |                        |                |               |               |                |        |         |            |         |       |             |
|                  VariableSizedArray_Builtin |     VariableSizedArray |   356,489.5 μs |   3,061.89 μs |   2,714.29 μs |   356,174.5 μs |   1.00 |    0.00 | 77000.0000 |       - |     - | 405333840 B |
|               VariableSizedArray_NewBuilder |     VariableSizedArray |   161,909.3 μs |     861.02 μs |     672.23 μs |   161,860.8 μs |   0.45 |    0.00 | 59000.0000 |       - |     - | 309333476 B |
|                                             |                        |                |               |               |                |        |         |            |         |       |             |
|                      FixedSizeArray_Builtin |        FixedSizedArray |    32,944.8 μs |     777.85 μs |   2,293.52 μs |    32,391.8 μs |   1.00 |    0.00 | 19875.0000 |       - |     - | 104000000 B |
|                   FixedSizeArray_NewBuilder |        FixedSizedArray |   219,352.6 μs |   4,288.40 μs |  10,837.34 μs |   217,830.4 μs |   6.65 |    0.60 | 82333.3333 |       - |     - | 432000000 B |
|                                             |                        |                |               |               |                |        |         |            |         |       |             |
|                  MultiStepOption_OldBuilder |        MultiStepOption |    63,360.5 μs |   1,199.04 μs |   1,062.92 μs |    63,133.5 μs |   1.00 |    0.00 | 38750.0000 |       - |     - | 202666703 B |
|                  MultiStepOption_NewBuilder |        MultiStepOption |    20,179.8 μs |     622.44 μs |   1,775.86 μs |    19,705.5 μs |   0.29 |    0.02 | 13437.5000 |       - |     - |  70399968 B |
|                   MultiStepOption_NoBuilder |        MultiStepOption |    19,727.8 μs |     469.72 μs |   1,362.75 μs |    19,395.3 μs |   0.32 |    0.02 | 13437.5000 |       - |     - |  70399968 B |
|                                             |                        |                |               |               |                |        |         |            |         |       |             |
|             MultiStepValueOption_OldBuilder |   MultiStepValueOption |    47,237.3 μs |     909.93 μs |     759.83 μs |    47,211.0 μs |   1.00 |    0.00 | 19090.9091 |       - |     - | 100266664 B |
|             MultiStepValueOption_NewBuilder |   MultiStepValueOption |     4,144.6 μs |      46.31 μs |      43.32 μs |     4,146.1 μs |   0.09 |    0.00 |          - |       - |     - |           - |
|              MultiStepValueOption_NoBuilder |   MultiStepValueOption |     3,824.0 μs |      75.26 μs |      73.92 μs |     3,806.3 μs |   0.08 |    0.00 |          - |       - |     - |           - |
|                                             |                        |                |               |               |                |        |         |            |         |       |             |
| NestedForLoops_taskSeqUsingRawResumableCode |                taskSeq |       983.7 μs |      18.23 μs |      17.90 μs |       984.7 μs |   1.61 |    0.04 |    54.6875 |       - |     - |    295641 B |
|        NestedForLoops_CSharpAsyncEnumerable |                taskSeq |       612.9 μs |      10.04 μs |       8.90 μs |       615.5 μs |   1.00 |    0.00 |    24.4141 |       - |     - |    131280 B |
