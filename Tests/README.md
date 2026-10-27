# 라이브러리 회귀 테스트

`PschLib.Runtime.Tests`는 Unity EditMode용 NUnit 테스트 어셈블리입니다.
UnityEngine 객체, 씬, 외부 패키지 없이 Core의 공개 API를 검증합니다.

## 실행

1. Unity 프로젝트에 Test Framework를 설치합니다. 현재 개발 프로젝트에는 설치되어 있습니다.
2. `Window > General > Test Runner`에서 `EditMode`를 선택합니다.
3. `EditMode` 탭에서 아래 테스트 어셈블리를 펼치고 `Run All` 또는 선택 실행합니다.

- `PschLib.Runtime.Tests`: EventBus, TimerScheduler, StateMachine.
- `PschLib.GoogleSheets.Tests`: 로컬 enum의 숫자 값 유지, 기존 중첩 enum 참조, 생성 파일 저장 실패 시 복원.
- `PschLib.Unity.UI.Tests`: 팝업 콜백 중 재진입 차단, 파괴된 팝업 정리. uGUI 설치 시 활성화됩니다.
- `PschLib.AssetLoading.Tests`: 같은 실제 에셋에 대한 여러 캐시 항목의 언로드 보호.
  UniTask 설치 시 활성화됩니다.

4. `PlayMode` 탭으로 바꾸고 `PschLib.Unity.Runtime.PlayMode.Tests`를 실행합니다.
   풀링과 타이머 러너의 실제 GameObject·Update·Destroy 동작을 검사합니다.
   테스트가 필요한 객체를 직접 만들고 정리하므로 씬에 테스트용 컴포넌트를 붙일 필요가 없습니다.

테스트 코드는 다음 파일에 있습니다. 메서드 이름이 Test Runner의 사례 이름으로 표시됩니다.

| 탭 | 파일 | 검사 대상 |
| --- | --- | --- |
| EditMode | `Editor/EventBusTests.cs`, `Editor/StateMachineTests.cs`, `Editor/TimerSchedulerTests.cs` | Core 로직 |
| EditMode | `GoogleSheet/Editor/*Tests.cs` | 시트 생성·enum·파일 복원 |
| EditMode | `UI/Editor/PopupHostTests.cs` | 팝업 수명과 재진입 |
| EditMode | `AssetLoading/Editor/AssetCacheTests.cs` | 캐시 소유권, UniTask 필요 |
| PlayMode | `UnityRuntime/PlayMode/PrefabPoolTests.cs` | 반환·재사용·개수 제한·실제 파괴 |
| PlayMode | `UnityRuntime/PlayMode/PrefabPoolManagerTests.cs` | 중복 키·잘못된 반환·Clear·Remove |
| PlayMode | `UnityRuntime/PlayMode/TimerSchedulerRunnerTests.cs` | Update 실행·timeScale·예외·파괴 시 취소 |

목록이 비어 있으면 Console의 컴파일 오류를 먼저 해결하고 Test Framework 설치 및 `.asmdef` 임포트를 확인합니다.
UniTask가 없는 현재 프로젝트에서는 `PschLib.AssetLoading.Tests` 9개가 표시되지 않는 것이 정상입니다.

UPM으로 설치한 프로젝트에서는 프로젝트의 `Packages/manifest.json`에
`"testables": ["com.psch.pschlib"]`를 최상위 항목으로 추가합니다.
기존 `testables` 배열이 있으면 패키지 이름만 추가합니다.

## 범위

| 대상 | 개수 | 주요 사례 |
| --- | ---: | --- |
| EventBus | 8 | 타입별 전달, 중복 구독 해제, 발행 중 구독/해제, 중첩 발행과 Clear, 예외 후 복구 |
| TimerScheduler | 17 | 입력 검증, 시간 모드, 정지/재개/취소, 콜백 중 추가/Clear, 재귀 Tick, 단일·복수 예외, 반복 횟수와 초과 시간 보존 |
| StateMachine | 9 | 생명주기 검증, 지연 전환과 우선순위, Enter 중 전환 요청, Enter/Exit 실패와 재시작 |
| PopupHost | 10 | 재진입, 예외 후 재시도, 파괴된 상단·숨겨진 팝업 정리, 같은 타입 재등록, 초기화·닫기 중 파괴 |
| Google Sheet enum | 13 | 숫자 값 유지, 중첩 enum의 C#/CLR 표기, enum 목록, 키워드 이름, 비공개·제네릭 타입 거부 |
| Resources AssetCache | 9 | 다른 타입·경로의 활성 참조, 대기자 보호, 중복 언로드 방지, 완료 통지용 임시 보호 해제 |
| 생성 파일 저장 | 6 | 원본 바이트와 GUID 복원, 중복 저장, 복원 실패 보고, 두 번째 파일 잠금, 진행 콜백 실패, 단일 시트 실패 |
| PrefabPool (PlayMode) | 8 | 입력 검증, Prewarm, 재사용·변환 복원, 잘못된 반환, 활성화, Clear, 용량 초과, 외부 파괴 |
| PrefabPoolManager (PlayMode) | 4 | 중복 등록, 외부 객체 반환 거부, 사용 중 제거 방지, Clear 후 재사용 |
| TimerSchedulerRunner (PlayMode) | 6 | Update, timeScale, Clear, OnDestroy, 예외 격리, 반복 횟수 |

EventBus는 정적 상태를 사용하므로 각 테스트 전후에 초기화합니다.
다른 테스트와 EventBus 상태를 공유하거나 병렬 실행하지 마세요.

## 보장하는 오류 처리 동작

- EventBus: Clear는 기존 구독과 보류 중인 구독을 제거하며, 중첩 발행을 포함해
  실행 중인 발행에서 아직 호출하지 않은 기존 리스너를 건너뜁니다.
  이미 실행 중인 핸들러 자체를 중단하지는 않습니다.
  Clear 이후 추가한 구독은 가장 바깥 Publish가 종료된 뒤 활성화됩니다.
- TimerScheduler: 콜백 예외가 발생해도 나머지 타이머를 처리하고 내부 정리를 마친 뒤
  호출자에게 전달합니다. 단일 예외는 원래 예외와 스택을 보존하며,
  여러 예외는 AggregateException.InnerExceptions에 보관합니다.
  Clear 요청은 예외 여부와 관계없이 남은 타이머를 취소합니다.
- StateMachine: 최초 Enter 또는 전환 중 Exit/Enter가 실패하면 현재 상태와 보류 요청을
  초기화하여 IsStarted를 false로 만들고 원래 예외를 전달합니다.
  실패한 전환에 StateChanged를 발행하지 않으며, Start로 다시 시작할 수 있습니다.
  실패한 생명주기 콜백을 재호출하거나 이전 상태로 자동 복구하지 않습니다.
  Enter가 부분적으로 생성한 리소스와 사용자 코드의 부수 효과는 해당 상태 구현에서
  정리해야 합니다. 일반 Update나 StateChanged 구독자의 예외는 이 초기화 정책의 대상이 아닙니다.

총 90개 테스트 사례(EditMode 72개 + PlayMode 18개)입니다. 매개변수 테스트는 각 인자를 별도 사례로 계산합니다.
모든 선택 패키지가 설치됐을 때의 수이며, UniTask가 없는 현재 프로젝트에서는 81개가 대상입니다.

## 사용 계약

- 반복 타이머는 `ScheduleRepeating`으로 생성합니다. 밀린 시간을 다음 실행에 보존하려면
  `overflowMode: TimerOverflowMode.Preserve`를 지정합니다.
- PopupHost가 열기·닫기·갱신·등록을 처리하는 동안 콜백에서 요청한 변경 작업은
  경고와 함께 `false` 또는 `null`을 반환합니다. 다른 팝업으로 이어지는 흐름은
  현재 작업이 반환된 뒤 호출합니다.
- 파괴된 팝업은 프레임 갱신과 조회·변경 작업 전에 정리합니다. 살아 있는 팝업의 순서를
  유지하고 표시 상태를 복원하며, 파괴된 팝업과 같은 타입을 다시 등록할 수 있습니다.
- ResourcesLoader는 같은 실제 에셋을 가리키는 캐시 항목이 모두 반환된 뒤 언로드합니다.
  이 소유권 보장은 같은 로더 인스턴스 안에서 적용됩니다.
- ResourcesLoader와 AddressablesLoader는 공유 로딩의 완료 통지가 끝날 때까지 에셋을 임시 보호합니다.
  먼저 완료된 호출자가 반환·정리해도 다른 대기자의 결과를 유지합니다. `Clear()`로 강제 초기화하면
  이전 세대의 미완료 호출은 `null`을 반환하며 같은 경로의 새 로딩 결과를 획득하지 않습니다.
- 기존 중첩 enum은 `enum<Namespace.Outer.Kind>` 또는 CLR 표기 `enum<Namespace.Outer+Kind>`로
  지정할 수 있습니다. 생성 코드는 검증된 런타임 타입에서 C# 이름을 만듭니다. enum과 바깥 타입은
  모두 public이어야 하며 제네릭 타입 내부 enum은 지원하지 않습니다.
- 시트 가져오기는 기존 `.Data.g.cs`의 로컬 enum 숫자를 유지합니다. 삭제된 멤버도 유지하고
  새 멤버는 기존 최댓값 다음에 추가합니다. 기존 생성 파일을 버전 관리에 포함해야 합니다.
  파일 삭제, 출력 경로·타입 이름 변경, enum 필드 삭제 후 재생성은 별도의 데이터 이전이 필요합니다.
  `SheetDataCodeGenerator.TryGenerate`를 직접 호출할 때는 이전 코드 인자를 전달해야 합니다.
- 생성 파일 저장 중 예외가 발생하면 같은 작업에서 변경·삭제한 파일과 기존 GUID를 복원합니다.
  기존 사용자 함수 파일은 덮어쓰지 않습니다. 파일 잠금 등으로 복원 자체가 실패하면
  원래 예외와 복원 실패 경로를 함께 보고합니다. 프로세스 강제 종료·전원 장애의 복구는 보장하지 않습니다.

## 이번 수정의 검증 기록

- 원본 소스로 Core 34개를 별도 .NET 러너에서 실행했습니다.
- enum 13개와 캐시 9개는 같은 러너에서 실행했습니다. 캐시는 Unity 객체를 대체한 객체로
  소유권 로직을 검증하므로 실제 Unity 네이티브 객체의 수명 검증을 대체하지 않습니다.
- 생성 파일 저장 6개도 실행해 .NET 테스트 총 62개가 통과했습니다. 실제 파일 잠금으로
  두 번째 시트 저장을 실패시켜 앞선 파일·공유 enum·기존 GUID 복원을 확인했습니다.
- 충돌하기 쉬운 시트 이름 10개로 실제 생성한 데이터·테이블 코드 20개를 별도 컴파일했습니다.
- 팝업 재진입·파괴 처리 검사 8개는 Unity 객체의 수명을 흉내 낸 대체 객체로 통과했습니다.
- 두 로더와 캐시의 실제 소스에 대체 Unity/UniTask/Addressables API를 연결해 공유 대기자,
  전체 취소, 완료 통지 중 Clear 후 재로딩 등 6개 시나리오를 별도로 검증했습니다.
- 최상위·중첩·키워드 이름 enum 및 enum 목록으로 실제 생성한 C# 코드 12개를 추가 컴파일했습니다.
- 새 PlayMode 테스트 18개는 실제 Unity 참조를 사용한 로컬 컴파일을 확인했습니다.
  아래 라이선스 제약으로 실제 PlayMode 실행 결과는 아직 없습니다.
- Unity EditMode 실행은 설치된 에디터의 라이선스 오류(종료 코드 198)로 시작되지 않았습니다.
  Unity Test Runner에서 전체 테스트를 실행하는 검증이 남아 있습니다.
