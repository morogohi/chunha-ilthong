# 천하일통 (天下一統)

먹으로 그린 지도 위에서 한 달을 한 턴으로 진행하는 삼국 전략 시뮬레이션. 세로 화면, 한 손 조작, 오프라인.

- **홍보 사이트**: https://morogohi.github.io/chunha-ilthong/
- **브라우저 체험판**: https://morogohi.github.io/chunha-ilthong/play/

| 폴더 | 내용 |
| --- | --- |
| [`unity/`](unity/) | 모바일 본편 (Unity 6.3 LTS, iOS·Android). 그래픽·사운드 모두 코드와 셰이더로 생성 — [README](unity/README.md) |
| [`web-prototype/`](web-prototype/) | 규칙 원본이자 브라우저 체험판 (TypeScript + Vite) |
| [`site/`](site/) | 홍보 웹페이지, 개인정보 처리방침, 지원 페이지 (GitHub Pages로 배포) |
| [`site-tests/`](site-tests/) | 홍보 사이트 E2E 테스트 (Playwright) |
| [`docs/`](docs/) | 스토어 등록 문안(한·영), 출시 체크리스트, 출시·화제성 계획 |

## 검증 방식

Unity 게임 로직(`unity/Assets/Chunha/Core`)은 UnityEngine을 참조하지 않는 순수 C#이라 .NET으로 바로 테스트한다.
같은 시드에서 TypeScript 원본과 **비트 단위로 같은 결과**가 나오는지 CI가 매번 확인한다.

| 검사 | 명령 | 내용 |
| --- | --- | --- |
| 코어 로직 | `cd unity/Tests/Chunha.Tests && dotnet run -c Release` | 단위·업적·AI 30년 시뮬레이션·TS 원본과의 일치 (44개) |
| Unity 스크립트 컴파일 | `cd unity/Tests/UnityStubCheck && dotnet build` | UnityEngine 대체 선언으로 Presentation·Editor 컴파일 |
| 합성 사운드 | `cd unity/Tests/AudioRender && dotnet run -- out` | 효과음 9종 + BGM을 WAV로 렌더, 클리핑 검사 |
| 체험판 | `cd web-prototype && npm ci && npm test && npx playwright test` | 단위·시뮬레이션 83개, 모바일 E2E 16개 |
| 홍보 사이트 | `cd site-tests && npm install && npx playwright test` | 모바일·데스크톱 E2E 12개 |

Unity 빌드는 라이선스가 필요해 수동 워크플로(`Unity build (manual)`)로 분리했다. 저장소 Secrets에 `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`를 넣고 Actions 탭에서 실행한다.

## 저작권

게임 코드·기획·아트 © 2026 천하일통 개발팀. All rights reserved. 이 저장소는 열람용으로 공개되며 별도 허락 없이 재배포·상업적 이용을 할 수 없다.
포함된 글꼴 Noto Sans CJK KR은 SIL Open Font License 1.1을 따른다 (`unity/Assets/Chunha/Resources/Fonts/OFL-1.1.txt`).
