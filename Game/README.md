# AO Play

AO 플레이 영상과 최종 발표 PDF를 포트폴리오용으로 정리한 패키지입니다.

## 구성

- `AO_최종ppt.pdf`: 최종 발표 자료
- `assets/AO_Play-Preview.mp4`: 저장소에 넣을 수 있게 추가 압축한 미리보기 영상
- `assets/AO_Play-Trim.mp4`: Release asset으로 올릴 고화질 압축본

## 업로드 방식

- PDF는 저장소에 직접 포함
- 짧은 미리보기 영상만 저장소에 포함
- 고화질 `AO_Play-Trim.mp4`는 GitHub Release asset으로 첨부
- 원본 `AO_Play.mp4`는 로컬 보관용으로만 유지

## 권장 저장소 구조

```text
AO_Play/
├── README.md
├── AO_최종ppt.pdf
└── assets/
    ├── AO_Play-Preview.mp4
    └── AO_Play-Trim.mp4
```

## 포트폴리오 문구 예시

```text
AO는 VR 리듬 액션 프로젝트로, Quest 3 단독 실행을 목표로 D-Variant 판정 구조와 리듬/연출/피버 시스템을 통합했습니다.
```

## 업로드 순서

1. 새 GitHub 저장소 생성
2. 위 구조대로 PDF와 README를 먼저 푸시
3. `AO_Play-Trim.mp4`는 Release를 만들어 asset으로 업로드
4. README에서 Release 링크를 함께 안내
