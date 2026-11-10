# 출시 체크리스트

스토어에 올리기 전에 위에서부터 차례로 확인한다. 정책은 바뀌므로 제출 직전에 각 콘솔의 최신 안내를 다시 본다.

## 1. 계정·법무

- [ ] Apple Developer Program 가입 (개인 또는 법인). 법인이면 D-U-N-S 번호 필요
- [ ] Google Play Console 개발자 계정. 2023년 11월 이후 만든 개인 계정은 출시 전 비공개 테스트(테스터 12명 이상, 14일 이상) 조건이 있다 — 일정에 반영
- [ ] 국내 출시: 게임물관리위원회 등급분류. 오픈마켓 자체등급분류 사업자(App Store·Google Play)를 통한 등급 부여로 갈음 가능한지 확인
- [ ] 상표: "천하일통" 상표 출원 여부 검토 (게임 소프트웨어 류)
- [ ] 글꼴: Noto Sans CJK KR (SIL OFL 1.1) — `Resources/Fonts/OFL-1.1.txt` 동봉, 앱 내 '오픈소스 라이선스'에 표기
- [ ] 역사 인물 이름은 저작권 대상이 아니다. 단, 타사 게임의 고유 연출·이미지·능력치 표를 그대로 쓰지 않았는지 최종 확인 (현재 능력치는 자체 산정)

## 2. 빌드 설정 (Unity)

- [ ] 번들 ID `com.chunha.ilthong` → 실제 소유 도메인 기준으로 바꿀지 결정 (출시 후 변경 불가)
- [ ] 버전 `0.2.0` → 출시 시 `1.0.0`, 빌드 번호 증가
- [ ] Android: IL2CPP, ARM64, 대상 API 레벨은 Play 정책의 최신 요구 수준, AAB(App Bundle)로 빌드, 업로드 키 서명 + Play 앱 서명
- [ ] iOS: 최소 iOS 버전, 세로 고정, 상태 표시줄 숨김, 런치 스크린(먹색 바탕 + 로고)
- [ ] 앱 아이콘: `site/assets/icon-1024.png` 원본 → 플랫폼별 아이콘 세트 생성 (Android 적응형 아이콘은 전경/배경 분리)
- [ ] Project Settings › Services: Unity Analytics·Cloud Diagnostics 꺼짐 확인 (개인정보 라벨 "수집 안 함"과 일치해야 함)
- [ ] iOS 개인정보 매니페스트: PlayerPrefs가 UserDefaults를 쓰므로 Required Reason API 항목이 Unity가 생성한 `PrivacyInfo.xcprivacy`에 들어갔는지 Xcode에서 확인
- [ ] Development Build 꺼짐, 스크립트 디버깅 꺼짐, 로그 수준 Error 이상

## 3. 스토어 정보

- [ ] 문안: [listing-ko.md](listing-ko.md), [listing-en.md](listing-en.md)
- [ ] 개인정보 처리방침 URL: `https://morogohi.github.io/chunha-ilthong/privacy.html`
- [ ] App Store 개인정보 라벨: "데이터를 수집하지 않음"
- [ ] Google Play 데이터 보안 양식: 수집 없음, 공유 없음, 전송 암호화 해당 없음(네트워크 미사용)
- [ ] 연령 등급 설문(IARC / App Store): 만화적 폭력 — 경미(병사 쓰러짐, 피 표현 없음), 처형 선택지 있음(텍스트만) → 예상 12세 이용가 / 9+ ~ 12+. 도박 요소·결제·광고·사용자 간 소통 없음
- [ ] 스크린샷 6장 + 미리보기 영상 (listing-ko.md 의 순서)
- [ ] 리뷰어용 메모: "튜토리얼은 첫 실행 때 자동 시작. 설정 › 튜토리얼 다시 보기로 재생 가능. 로그인·계정 없음."

## 4. 품질 게이트 (출시 전 반드시)

- [ ] CI 전부 통과 (`.github/workflows/ci.yml`)
- [ ] 실기기 테스트: iPhone SE(작은 화면)·iPhone 15 Pro·중저가 Android(RAM 3GB)·태블릿 1대
  - 시작~첫 턴 종료까지 크래시 없음, 30분 연속 플레이 메모리 증가 없음
  - 60fps 유지(지도 이동·전투), 저사양에서 30fps 이상
  - 노치·홈 인디케이터에 UI가 가리지 않음 (Safe Area)
  - 소리 끄기, 진동 끄기, 백그라운드 복귀, 전화 수신 후 복귀, 저장·불러오기
- [ ] 한 판을 끝까지(천하통일·멸망 각각) 플레이해 엔딩 화면 확인
- [ ] 비공개 테스트(TestFlight / Play 비공개 트랙) 2주, 피드백 반영

## 5. 출시 후

- [ ] 크래시 모니터링: 수집을 하지 않으므로 스토어 콘솔(App Store Connect 충돌, Play Android vitals)만 사용. 별도 SDK를 넣으면 개인정보 라벨을 다시 써야 함
- [ ] 리뷰 응답 (첫 2주는 매일)
- [ ] 1.1 업데이트 계획: 장수 원화, 시나리오 추가(황건·적벽), 클라우드 저장
