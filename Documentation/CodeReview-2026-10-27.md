# 전체 C# 코드 검토 — 2026-10-27

전체 검토 당시 작업 트리(앞선 수정 포함)의 `.cs` 98개, 12,038줄을 대상으로 상세 검토했습니다.
제품 코드 90개와 테스트 코드 8개를 포함합니다. 최초 검토는 소스 변경 없이 수행했고, 이후 F01/F02를 수정했습니다.

검토 완료는 결함이 없다는 보증이 아닙니다. 코드 읽기, 로컬 컴파일, 대체 환경 재현, 실제 Unity 실행을 구분했습니다.

## 후속 수정 완료

F01/F02를 모두 수정했습니다. 아래 결함 설명·출력·파일별 SHA-256 목록은 발견 당시의 기록입니다.
현재 소스는 새 enum 테스트 파일을 포함해 99개, 12,323줄입니다.

- F01: 두 캐시에 임시 보호 기능을 추가해 완료 통지 중 Release/Unload/ClearUnused가 나머지 대기자의 에셋을 해제하지 않도록 했습니다.
  임시 보호는 캐시 항목 자체를 참조하므로 Clear 후 같은 경로에 생성된 새 항목의 참조 횟수에 영향을 주지 않습니다.
  각 pending 요청에 세대를 기록하여 이전 대기자가 새 세대의 에셋을 획득하는 것도 막았습니다.
- F02: C#/CLR 형식의 중첩 enum 이름을 해석하고, 검증된 Type을 기준으로 C# 타입 이름을 생성합니다.
  키워드 이름은 @로 이스케이프하고, 비공개 바깥 타입과 제네릭 내부 enum은 생성 전에 거부합니다.
- 회귀 검증: .NET 테스트 62개 통과. 실제 두 로더·캐시 소스와 대체 API를 사용한 6개 시나리오 통과.
  새 enum 생성 코드 12개 컴파일 통과. Unity/.NET 로컬 컴파일도 다시 확인했습니다.
- 실제 Unity/UniTask/Addressables 패키지 환경에서의 실행 검증은 기존 라이선스·패키지 제약으로 여전히 남아 있습니다.
## 추가로 확인한 결함

### F01 · P1 · 공유 비동기 로딩 완료 통지 중 대기자의 에셋 소유권이 사라질 수 있음

- 위치: `Unity/AssetLoading/Runtime/Resources/ResourcesLoader.cs:118`, `:253`, `:257`.
- 같은 순서가 있는 위치: `Unity/AssetLoading/Addressables/Runtime/AddressablesLoader.cs:98`, `:142`, `:152`.
- 상태: 수정 완료. 아래는 수정 전 재현 기록입니다. 후속 검증에서는 두 로더 모두 실제 소스와 대체 API를 사용해 수정 결과를 확인했습니다.

현재는 로딩 결과를 참조 횟수 0으로 캐시에 넣고, pending 항목을 제거한 다음 모든 대기자에게 완료를 통지합니다.
각 대기자는 완료 통지를 받은 뒤 `TryAcquire`로 자기 참조를 확보합니다.

발생 순서:

1. A와 B가 같은 경로를 요청하고 하나의 로딩 완료를 기다립니다.
2. 로딩이 성공해 캐시에 참조 횟수 0으로 들어갑니다.
3. A가 먼저 재개해 에셋을 얻습니다.
4. A의 후속 코드가 곧바로 `Release`와 `ClearUnused`를 호출합니다.
5. B의 참조가 아직 잡히지 않았으므로 에셋이 캐시에서 제거됩니다.
6. B는 `TryAcquire`에 실패해, 같은 로딩이 성공했음에도 `null`을 받습니다.

대체 환경 출력:

```text
Shared native request count=1
First consumer released and ClearUnused unloaded=1
Second consumer received null=True
```

UniTask의 완료 소스가 등록된 continuation을 순서대로 직접 호출하는 점은
[공식 UniTaskCompletionSource 구현](https://github.com/Cysharp/UniTask/blob/master/src/UniTask/Assets/Plugins/UniTask/Runtime/UniTaskCompletionSource.cs)에서도 확인했습니다.
설치할 실제 패키지 버전과 Unity PlayerLoop에서의 재현은 추가 검증 대상입니다.

수정 방향: 완료 통지 동안 내부 소유권을 유지하거나 대기자별 소유권을 먼저 예약해,
첫 대기자의 반환·정리가 나머지 대기자의 획득에 영향을 주지 않도록 해야 합니다.
취소된 대기자의 예약 해제도 함께 처리해야 합니다.

재현 파일은 작업 산출물 폴더의 `review/full-audit/AsyncLoaderAudit.cs`입니다. 대체 API로 수행한 로직 검사이며 실제 Unity 실행 결과가 아닙니다.

### F02 · P2 · 기존 중첩 enum 참조로 생성한 C#이 컴파일되지 않음

- 위치: `Unity/Editor/GoogleSheet/Models/SheetTypeCatalog.cs:118`, `Unity/Editor/GoogleSheet/Generation/SheetDataCodeGenerator.cs:439`, `:525`.
- 상태: 수정 완료. 아래는 수정 전 재현 기록입니다. 수정 후 중첩 enum과 enum 목록의 실제 생성 코드 컴파일을 확인했습니다.

예시:

```csharp
public class Outer
{
    public enum Kind { One }
}
```

`enum<Outer.Kind>`는 `Assembly.GetType`으로 찾지 못해 거부됩니다.
CLR 표기인 `enum<Outer+Kind>`는 허용되지만 생성기에 그대로 전달되어 다음 코드가 나옵니다.

```csharp
public global::Outer+Kind kind;
```

이 코드는 `CS1519`로 컴파일되지 않습니다. 일반 네임스페이스 최상위 enum에는 이 문제가 없습니다.

수정 방향: 입력 enum을 `Type`으로 확인한 후 C#에서 사용할 수 있는 타입 이름으로 변환해야 합니다.
바깥 타입의 접근 가능 여부와 제네릭 여부도 확인하고, 미지원 형태는 파일 저장 전에 명확하게 거부해야 합니다.

재현 파일은 작업 산출물 폴더의 `review/full-audit/GenerationEdgeAudit.cs`와 `NestedEnum.Data.g.cs`입니다.

## 개선 제안 — 확정 결함과 구분

### I01 · CanvasGroup을 통한 버튼 비활성화도 표시 상태에 반영할지 결정

`Unity/UI/Runtime/ButtonView.cs:79`, `:106`은 `Button.interactable`만 확인합니다.
부모 `CanvasGroup.interactable = false`이면 실제 클릭 가능 상태와 라벨 색·잠금 표시가 달라질 수 있습니다.
설치된 uGUI의 `Selectable.IsInteractable()` 구현은 그룹 상태와 버튼 자체 상태를 함께 확인합니다.

버튼 자체 플래그만 표시하는 도구가 의도라면 현재 동작을 문서화하고,
실제 클릭 가능 상태를 표시하려는 도구라면 `IsInteractable()`의 변화를 관찰하도록 개선할 수 있습니다.
실제 UI 실행 재현은 하지 않았으므로 확정 결함 수에는 포함하지 않았습니다.

## 검증 범위

- 실제 설치된 Unity/.NET 참조로 95개 소스의 로컬 컴파일을 확인했습니다. 어셈블리를 나누어 검사한 결과이며 완전한 Unity 프로젝트 빌드 결과는 아닙니다.
- 다음 3개는 실제 UniTask/Addressables 종속성을 사용한 컴파일이 미완료입니다.
  - `Unity/AssetLoading/Runtime/Resources/ResourcesLoader.cs`
  - `Unity/AssetLoading/Addressables/Runtime/AddressablesLoader.cs`
  - `Unity/AssetLoading/Addressables/Runtime/Internal/AddressableAssetCache.cs`
- 위 3개도 소스 전체를 검토했습니다. ResourcesLoader는 추가로 대체 API를 연결한 컴파일·실행 재현을 수행했습니다.
- AssetLoading의 에디터·디버그 코드 및 Addressables의 순수 키·열거형 코드는 별도로 컴파일했습니다.
- Core·enum·캐시·파일 저장 관련 기존 테스트 52개가 별도 .NET 러너에서 다시 통과했습니다. Unity 객체가 필요한 테스트에는 대체 객체를 사용했습니다.
- 앞서 만든 생성 코드 20개 컴파일 검사와 팝업 대체 객체 검사 8개 결과도 확인 범위에 포함합니다.
- Unity 에디터 라이선스 오류(198)로 실제 EditMode 테스트와 플레이어 빌드는 실행하지 못했습니다.
- 현재 테스트 통과는 F01/F02가 없다는 의미가 아닙니다. 이 두 경로는 기존 테스트에서 다루지 않았습니다.

## 발견 당시 파일별 상세 검토 목록

아래 SHA-256은 검토 대상 소스를 구분하기 위한 앞 12자리입니다.
`로컬 컴파일`은 실제 설치된 Unity/.NET 참조를 사용한 정적 컴파일이며 엔진 실행 검증과 다릅니다.
추가 발견 표시가 없는 파일에서도 입력·예외·수명·조건부 컴파일 및 호출 관계를 검토했습니다.

| 파일 | 줄 수 | SHA-256 앞 12자리 | 상세 검토 | 컴파일 범위 | 추가 발견 |
| --- | ---: | --- | --- | --- | --- |
| [Runtime/Debugging/DebugObserverExceptionReporter.cs](/D:/GitHub/PschLib/Assets/Scripts/Runtime/Debugging/DebugObserverExceptionReporter.cs) | 59 | E2BE29E3D668 | 완료 | 로컬 컴파일 | — |
| [Runtime/Messaging/EventBus.cs](/D:/GitHub/PschLib/Assets/Scripts/Runtime/Messaging/EventBus.cs) | 363 | AB5B95289922 | 완료 | 로컬 컴파일 | — |
| [Runtime/Scheduling/ITimerSchedulerDebugInfo.cs](/D:/GitHub/PschLib/Assets/Scripts/Runtime/Scheduling/ITimerSchedulerDebugInfo.cs) | 16 | B6082C9E4018 | 완료 | 로컬 컴파일 | — |
| [Runtime/Scheduling/TimerDebugEntry.cs](/D:/GitHub/PschLib/Assets/Scripts/Runtime/Scheduling/TimerDebugEntry.cs) | 34 | 06290226A493 | 완료 | 로컬 컴파일 | — |
| [Runtime/Scheduling/TimerEntry.cs](/D:/GitHub/PschLib/Assets/Scripts/Runtime/Scheduling/TimerEntry.cs) | 109 | E9960C6CAA5C | 완료 | 로컬 컴파일 | — |
| [Runtime/Scheduling/TimerHandle.cs](/D:/GitHub/PschLib/Assets/Scripts/Runtime/Scheduling/TimerHandle.cs) | 120 | 093C091AFFFC | 완료 | 로컬 컴파일 | — |
| [Runtime/Scheduling/TimerOverflowMode.cs](/D:/GitHub/PschLib/Assets/Scripts/Runtime/Scheduling/TimerOverflowMode.cs) | 8 | 112A58F2146C | 완료 | 로컬 컴파일 | — |
| [Runtime/Scheduling/TimerScheduler.cs](/D:/GitHub/PschLib/Assets/Scripts/Runtime/Scheduling/TimerScheduler.cs) | 295 | 66707DA71E68 | 완료 | 로컬 컴파일 | — |
| [Runtime/Scheduling/TimerState.cs](/D:/GitHub/PschLib/Assets/Scripts/Runtime/Scheduling/TimerState.cs) | 11 | 1BEE1FF667C2 | 완료 | 로컬 컴파일 | — |
| [Runtime/Scheduling/TimerTimeMode.cs](/D:/GitHub/PschLib/Assets/Scripts/Runtime/Scheduling/TimerTimeMode.cs) | 9 | E956ACD7F6A1 | 완료 | 로컬 컴파일 | — |
| [Runtime/StateMachine/IState.cs](/D:/GitHub/PschLib/Assets/Scripts/Runtime/StateMachine/IState.cs) | 9 | 706DB4A374F8 | 완료 | 로컬 컴파일 | — |
| [Runtime/StateMachine/IStateMachineDebugInfo.cs](/D:/GitHub/PschLib/Assets/Scripts/Runtime/StateMachine/IStateMachineDebugInfo.cs) | 16 | 2B69F64F39FC | 완료 | 로컬 컴파일 | — |
| [Runtime/StateMachine/StateMachine.cs](/D:/GitHub/PschLib/Assets/Scripts/Runtime/StateMachine/StateMachine.cs) | 413 | 368BBEFFFC2F | 완료 | 로컬 컴파일 | — |
| [Tests/AssetLoading/Editor/AssetCacheTests.cs](/D:/GitHub/PschLib/Assets/Scripts/Tests/AssetLoading/Editor/AssetCacheTests.cs) | 87 | 1A8F275307FB | 완료 | 로컬 컴파일 | — |
| [Tests/Editor/EventBusTests.cs](/D:/GitHub/PschLib/Assets/Scripts/Tests/Editor/EventBusTests.cs) | 124 | B9170DB38F46 | 완료 | 로컬 컴파일 | — |
| [Tests/Editor/StateMachineTests.cs](/D:/GitHub/PschLib/Assets/Scripts/Tests/Editor/StateMachineTests.cs) | 161 | B834EA64B330 | 완료 | 로컬 컴파일 | — |
| [Tests/Editor/TimerSchedulerTests.cs](/D:/GitHub/PschLib/Assets/Scripts/Tests/Editor/TimerSchedulerTests.cs) | 265 | 070D5C63F41F | 완료 | 로컬 컴파일 | — |
| [Tests/GoogleSheet/Editor/GeneratedFileTransactionTests.cs](/D:/GitHub/PschLib/Assets/Scripts/Tests/GoogleSheet/Editor/GeneratedFileTransactionTests.cs) | 93 | 6A9B78C35F94 | 완료 | 로컬 컴파일 | — |
| [Tests/GoogleSheet/Editor/SheetCodeFileWriterTests.cs](/D:/GitHub/PschLib/Assets/Scripts/Tests/GoogleSheet/Editor/SheetCodeFileWriterTests.cs) | 108 | A9B123E4A9EB | 완료 | 로컬 컴파일 | — |
| [Tests/GoogleSheet/Editor/SheetEnumStabilityTests.cs](/D:/GitHub/PschLib/Assets/Scripts/Tests/GoogleSheet/Editor/SheetEnumStabilityTests.cs) | 63 | EC28D7750DE0 | 완료 | 로컬 컴파일 | — |
| [Tests/UI/Editor/PopupHostTests.cs](/D:/GitHub/PschLib/Assets/Scripts/Tests/UI/Editor/PopupHostTests.cs) | 212 | 763F2605BA18 | 완료 | 로컬 컴파일 | — |
| [Unity/AssetLoading/Addressables/Runtime/AddressablesLoader.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/AssetLoading/Addressables/Runtime/AddressablesLoader.cs) | 401 | E6169228A63F | 완료 | 실제 종속성 미확인 | F01 (정적 확인) |
| [Unity/AssetLoading/Addressables/Runtime/Internal/AddressableAssetCache.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/AssetLoading/Addressables/Runtime/Internal/AddressableAssetCache.cs) | 287 | 28201E273DF0 | 완료 | 실제 종속성 미확인 | — |
| [Unity/AssetLoading/Addressables/Runtime/Internal/AddressableAssetKey.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/AssetLoading/Addressables/Runtime/Internal/AddressableAssetKey.cs) | 39 | C30CF6230AE9 | 완료 | 로컬 컴파일 | — |
| [Unity/AssetLoading/Addressables/Runtime/Internal/AddressableReleaseResult.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/AssetLoading/Addressables/Runtime/Internal/AddressableReleaseResult.cs) | 10 | 3612D3262B33 | 완료 | 로컬 컴파일 | — |
| [Unity/AssetLoading/Addressables/Runtime/Internal/AddressableUnloadResult.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/AssetLoading/Addressables/Runtime/Internal/AddressableUnloadResult.cs) | 9 | CC066D4EDFCA | 완료 | 로컬 컴파일 | — |
| [Unity/AssetLoading/Editor/AssetLoaderDebugViewerBuildProcessor.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/AssetLoading/Editor/AssetLoaderDebugViewerBuildProcessor.cs) | 21 | E3BFAB304E52 | 완료 | 로컬 컴파일 | — |
| [Unity/AssetLoading/Editor/AssetLoaderDebugViewerEditor.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/AssetLoading/Editor/AssetLoaderDebugViewerEditor.cs) | 98 | 8BDEE45B1522 | 완료 | 로컬 컴파일 | — |
| [Unity/AssetLoading/Runtime/Debugging/AssetLoaderDebugEntry.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/AssetLoading/Runtime/Debugging/AssetLoaderDebugEntry.cs) | 18 | 58478461E8F8 | 완료 | 로컬 컴파일 | — |
| [Unity/AssetLoading/Runtime/Debugging/AssetLoaderDebugViewer.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/AssetLoading/Runtime/Debugging/AssetLoaderDebugViewer.cs) | 54 | 4260C6EE5E51 | 완료 | 로컬 컴파일 | — |
| [Unity/AssetLoading/Runtime/Debugging/IAssetLoaderDebugInfo.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/AssetLoading/Runtime/Debugging/IAssetLoaderDebugInfo.cs) | 18 | 0AEE130E6B0A | 완료 | 로컬 컴파일 | — |
| [Unity/AssetLoading/Runtime/Internal/AssemblyInfo.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/AssetLoading/Runtime/Internal/AssemblyInfo.cs) | 3 | 9026C8BC3C5B | 완료 | 로컬 컴파일 | — |
| [Unity/AssetLoading/Runtime/Internal/AssetCache.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/AssetLoading/Runtime/Internal/AssetCache.cs) | 337 | 32C9CF6646AC | 완료 | 로컬 컴파일 | — |
| [Unity/AssetLoading/Runtime/Internal/AssetKey.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/AssetLoading/Runtime/Internal/AssetKey.cs) | 37 | FF42DB11CF4A | 완료 | 로컬 컴파일 | — |
| [Unity/AssetLoading/Runtime/Internal/AssetReleaseResult.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/AssetLoading/Runtime/Internal/AssetReleaseResult.cs) | 10 | 9B4CB24237B7 | 완료 | 로컬 컴파일 | — |
| [Unity/AssetLoading/Runtime/Internal/AssetUnloadResult.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/AssetLoading/Runtime/Internal/AssetUnloadResult.cs) | 9 | BF15966D50A5 | 완료 | 로컬 컴파일 | — |
| [Unity/AssetLoading/Runtime/Resources/ResourcesLoader.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/AssetLoading/Runtime/Resources/ResourcesLoader.cs) | 383 | 9881695F1E07 | 완료 | 실제 종속성 미확인 | F01 (대체 환경 재현) |
| [Unity/Editor/AssemblyInfo.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Editor/AssemblyInfo.cs) | 3 | 1DD0FE2ECD25 | 완료 | 로컬 컴파일 | — |
| [Unity/Editor/Debugging/DebugObserverExceptionReporterInitializer.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Editor/Debugging/DebugObserverExceptionReporterInitializer.cs) | 32 | F9AB90A6D278 | 완료 | 로컬 컴파일 | — |
| [Unity/Editor/Debugging/DebugViewerBuildUtility.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Editor/Debugging/DebugViewerBuildUtility.cs) | 23 | A022B8E1FDF7 | 완료 | 로컬 컴파일 | — |
| [Unity/Editor/Debugging/DebugViewerEditorBase.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Editor/Debugging/DebugViewerEditorBase.cs) | 179 | A4E173F95246 | 완료 | 로컬 컴파일 | — |
| [Unity/Editor/GoogleSheet/Generation/GeneratedFileTransaction.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Editor/GoogleSheet/Generation/GeneratedFileTransaction.cs) | 132 | 146A8FE8A106 | 완료 | 로컬 컴파일 | — |
| [Unity/Editor/GoogleSheet/Generation/GoogleSheetPathUtility.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Editor/GoogleSheet/Generation/GoogleSheetPathUtility.cs) | 88 | D181A976A6B3 | 완료 | 로컬 컴파일 | — |
| [Unity/Editor/GoogleSheet/Generation/SheetAssetWriter.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Editor/GoogleSheet/Generation/SheetAssetWriter.cs) | 265 | DA0549C3274A | 완료 | 로컬 컴파일 | — |
| [Unity/Editor/GoogleSheet/Generation/SheetCodeFileWriter.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Editor/GoogleSheet/Generation/SheetCodeFileWriter.cs) | 433 | 6E07F2BE4A7A | 완료 | 로컬 컴파일 | — |
| [Unity/Editor/GoogleSheet/Generation/SheetDataCodeGenerator.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Editor/GoogleSheet/Generation/SheetDataCodeGenerator.cs) | 658 | A8223CAC3386 | 완료 | 로컬 컴파일 | F02 |
| [Unity/Editor/GoogleSheet/Generation/SheetSharedEnumCatalog.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Editor/GoogleSheet/Generation/SheetSharedEnumCatalog.cs) | 224 | 8DB1165D1BD0 | 완료 | 로컬 컴파일 | — |
| [Unity/Editor/GoogleSheet/GoogleSheetDocumentation.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Editor/GoogleSheet/GoogleSheetDocumentation.cs) | 110 | 6D23FEFF882E | 완료 | 로컬 컴파일 | — |
| [Unity/Editor/GoogleSheet/Importing/GoogleSheetDocumentLoader.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Editor/GoogleSheet/Importing/GoogleSheetDocumentLoader.cs) | 93 | 70BABBCA3C4D | 완료 | 로컬 컴파일 | — |
| [Unity/Editor/GoogleSheet/Importing/GoogleSheetImportService.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Editor/GoogleSheet/Importing/GoogleSheetImportService.cs) | 76 | E9895584C74F | 완료 | 로컬 컴파일 | — |
| [Unity/Editor/GoogleSheet/Importing/GoogleSheetPendingImportProcessor.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Editor/GoogleSheet/Importing/GoogleSheetPendingImportProcessor.cs) | 563 | 89587A356C6F | 완료 | 로컬 컴파일 | — |
| [Unity/Editor/GoogleSheet/Importing/GoogleSheetRegistryService.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Editor/GoogleSheet/Importing/GoogleSheetRegistryService.cs) | 382 | 7913478C1870 | 완료 | 로컬 컴파일 | — |
| [Unity/Editor/GoogleSheet/Models/GoogleSheetApiModels.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Editor/GoogleSheet/Models/GoogleSheetApiModels.cs) | 72 | A99D0F7D99C3 | 완료 | 로컬 컴파일 | — |
| [Unity/Editor/GoogleSheet/Models/GoogleSheetImportResult.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Editor/GoogleSheet/Models/GoogleSheetImportResult.cs) | 20 | 0F9ED0BED5FE | 완료 | 로컬 컴파일 | — |
| [Unity/Editor/GoogleSheet/Models/SheetDataRow.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Editor/GoogleSheet/Models/SheetDataRow.cs) | 18 | 4164C369C964 | 완료 | 로컬 컴파일 | — |
| [Unity/Editor/GoogleSheet/Models/SheetDocument.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Editor/GoogleSheet/Models/SheetDocument.cs) | 16 | 8D71CE9BC50C | 완료 | 로컬 컴파일 | — |
| [Unity/Editor/GoogleSheet/Models/SheetEnumMode.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Editor/GoogleSheet/Models/SheetEnumMode.cs) | 10 | FAB8A173594D | 완료 | 로컬 컴파일 | — |
| [Unity/Editor/GoogleSheet/Models/SheetField.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Editor/GoogleSheet/Models/SheetField.cs) | 19 | ABE8A45469FD | 완료 | 로컬 컴파일 | — |
| [Unity/Editor/GoogleSheet/Models/SheetScalarTypeDefinition.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Editor/GoogleSheet/Models/SheetScalarTypeDefinition.cs) | 18 | 72862E833D34 | 완료 | 로컬 컴파일 | — |
| [Unity/Editor/GoogleSheet/Models/SheetSharedEnumDefinition.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Editor/GoogleSheet/Models/SheetSharedEnumDefinition.cs) | 12 | 347E231D0468 | 완료 | 로컬 컴파일 | — |
| [Unity/Editor/GoogleSheet/Models/SheetTypeCatalog.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Editor/GoogleSheet/Models/SheetTypeCatalog.cs) | 182 | CBD12377577A | 완료 | 로컬 컴파일 | F02 |
| [Unity/Editor/GoogleSheet/Models/SheetTypeInfo.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Editor/GoogleSheet/Models/SheetTypeInfo.cs) | 28 | FA6A106AC276 | 완료 | 로컬 컴파일 | — |
| [Unity/Editor/GoogleSheet/Models/SheetTypeKind.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Editor/GoogleSheet/Models/SheetTypeKind.cs) | 10 | 667FD5F17E69 | 완료 | 로컬 컴파일 | — |
| [Unity/Editor/GoogleSheet/Networking/GoogleSheetWebClient.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Editor/GoogleSheet/Networking/GoogleSheetWebClient.cs) | 152 | C1D64B720527 | 완료 | 로컬 컴파일 | — |
| [Unity/Editor/GoogleSheet/Parsing/SheetDataParser.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Editor/GoogleSheet/Parsing/SheetDataParser.cs) | 122 | 19B99459149D | 완료 | 로컬 컴파일 | — |
| [Unity/Editor/GoogleSheet/Parsing/SheetHeaderParser.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Editor/GoogleSheet/Parsing/SheetHeaderParser.cs) | 46 | F6270F421A78 | 완료 | 로컬 컴파일 | — |
| [Unity/Editor/GoogleSheet/Parsing/SheetValueParser.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Editor/GoogleSheet/Parsing/SheetValueParser.cs) | 145 | BF4D54A963BC | 완료 | 로컬 컴파일 | — |
| [Unity/Editor/GoogleSheet/Settings/GoogleSheetEntry.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Editor/GoogleSheet/Settings/GoogleSheetEntry.cs) | 14 | FC45FC4714F4 | 완료 | 로컬 컴파일 | — |
| [Unity/Editor/GoogleSheet/Settings/GoogleSheetProject.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Editor/GoogleSheet/Settings/GoogleSheetProject.cs) | 31 | 17CAAD2C1AF6 | 완료 | 로컬 컴파일 | — |
| [Unity/Editor/GoogleSheet/Settings/GoogleSheetServer.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Editor/GoogleSheet/Settings/GoogleSheetServer.cs) | 17 | D1681B890964 | 완료 | 로컬 컴파일 | — |
| [Unity/Editor/GoogleSheet/Settings/GoogleSheetSettings.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Editor/GoogleSheet/Settings/GoogleSheetSettings.cs) | 18 | 7D2E21F49611 | 완료 | 로컬 컴파일 | — |
| [Unity/Editor/GoogleSheet/Windows/GoogleSheetGeneratorWindow.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Editor/GoogleSheet/Windows/GoogleSheetGeneratorWindow.cs) | 728 | 1E9EA8757420 | 완료 | 로컬 컴파일 | — |
| [Unity/Editor/GoogleSheet/Windows/GoogleSheetImportProgressWindow.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Editor/GoogleSheet/Windows/GoogleSheetImportProgressWindow.cs) | 66 | 6C3F9AD5E64A | 완료 | 로컬 컴파일 | — |
| [Unity/Editor/GoogleSheet/Windows/GoogleSheetProjectRegistrationWindow.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Editor/GoogleSheet/Windows/GoogleSheetProjectRegistrationWindow.cs) | 101 | 31342672A135 | 완료 | 로컬 컴파일 | — |
| [Unity/Editor/GoogleSheet/Windows/SheetTableEditor.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Editor/GoogleSheet/Windows/SheetTableEditor.cs) | 74 | 2C53CBA55048 | 완료 | 로컬 컴파일 | — |
| [Unity/Editor/Messaging/EventBusDebugViewerBuildProcessor.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Editor/Messaging/EventBusDebugViewerBuildProcessor.cs) | 21 | 8E0D7DEEC9B8 | 완료 | 로컬 컴파일 | — |
| [Unity/Editor/Messaging/EventBusDebugViewerEditor.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Editor/Messaging/EventBusDebugViewerEditor.cs) | 113 | 8B94F0A7D243 | 완료 | 로컬 컴파일 | — |
| [Unity/Editor/Pooling/PrefabPoolManagerEditor.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Editor/Pooling/PrefabPoolManagerEditor.cs) | 96 | 7E6D2D1BE4DC | 완료 | 로컬 컴파일 | — |
| [Unity/Editor/Scheduling/TimerSchedulerDebugViewerBuildProcessor.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Editor/Scheduling/TimerSchedulerDebugViewerBuildProcessor.cs) | 21 | 7987ECFE731E | 완료 | 로컬 컴파일 | — |
| [Unity/Editor/Scheduling/TimerSchedulerDebugViewerEditor.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Editor/Scheduling/TimerSchedulerDebugViewerEditor.cs) | 91 | 90E718387D29 | 완료 | 로컬 컴파일 | — |
| [Unity/Editor/StateMachine/StateMachineDebugViewerBuildProcessor.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Editor/StateMachine/StateMachineDebugViewerBuildProcessor.cs) | 19 | FF13107BB234 | 완료 | 로컬 컴파일 | — |
| [Unity/Editor/StateMachine/StateMachineDebugViewerEditor.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Editor/StateMachine/StateMachineDebugViewerEditor.cs) | 49 | 4D3FD4BC9799 | 완료 | 로컬 컴파일 | — |
| [Unity/Runtime/Components/DontDestroyOnLoadObject.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Runtime/Components/DontDestroyOnLoadObject.cs) | 24 | D1F318BE4DB7 | 완료 | 로컬 컴파일 | — |
| [Unity/Runtime/Components/StateMachineDebugViewer.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Runtime/Components/StateMachineDebugViewer.cs) | 57 | E992E5CAA81F | 완료 | 로컬 컴파일 | — |
| [Unity/Runtime/Messaging/EventBusDebugViewer.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Runtime/Messaging/EventBusDebugViewer.cs) | 25 | C0264703C8A9 | 완료 | 로컬 컴파일 | — |
| [Unity/Runtime/Pooling/PooledObjectTracker.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Runtime/Pooling/PooledObjectTracker.cs) | 51 | 05F9870B10A3 | 완료 | 로컬 컴파일 | — |
| [Unity/Runtime/Pooling/PrefabPool.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Runtime/Pooling/PrefabPool.cs) | 318 | 9FF68E103875 | 완료 | 로컬 컴파일 | — |
| [Unity/Runtime/Pooling/PrefabPoolManager.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Runtime/Pooling/PrefabPoolManager.cs) | 496 | 5298226AF32F | 완료 | 로컬 컴파일 | — |
| [Unity/Runtime/Pooling/PrefabPoolSetting.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Runtime/Pooling/PrefabPoolSetting.cs) | 19 | 86C4633A4DD6 | 완료 | 로컬 컴파일 | — |
| [Unity/Runtime/Scheduling/TimerSchedulerDebugViewer.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Runtime/Scheduling/TimerSchedulerDebugViewer.cs) | 61 | E02C1E849CF8 | 완료 | 로컬 컴파일 | — |
| [Unity/Runtime/Scheduling/TimerSchedulerRunner.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Runtime/Scheduling/TimerSchedulerRunner.cs) | 48 | C1DEF17934C4 | 완료 | 로컬 컴파일 | — |
| [Unity/Runtime/SheetTableBase.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Runtime/SheetTableBase.cs) | 10 | 9B442557C9B0 | 완료 | 로컬 컴파일 | — |
| [Unity/Runtime/Singleton.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/Runtime/Singleton.cs) | 48 | 517F1B91A800 | 완료 | 로컬 컴파일 | — |
| [Unity/UI/Editor/ButtonViewEditor.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/UI/Editor/ButtonViewEditor.cs) | 184 | D2F9DA9C37CC | 완료 | 로컬 컴파일 | — |
| [Unity/UI/Runtime/ButtonView.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/UI/Runtime/ButtonView.cs) | 227 | ACECCDDE936D | 완료 | 로컬 컴파일 | I01 (개선 제안) |
| [Unity/UI/Runtime/Popups/PopupBase.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/UI/Runtime/Popups/PopupBase.cs) | 113 | 92D9824502F9 | 완료 | 로컬 컴파일 | — |
| [Unity/UI/Runtime/Popups/PopupDisplayMode.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/UI/Runtime/Popups/PopupDisplayMode.cs) | 9 | 144932492B38 | 완료 | 로컬 컴파일 | — |
| [Unity/UI/Runtime/Popups/PopupHost.cs](/D:/GitHub/PschLib/Assets/Scripts/Unity/UI/Runtime/Popups/PopupHost.cs) | 449 | CEE079C340C3 | 완료 | 로컬 컴파일 | — |
