using UnityEngine;

/// <summary>
/// 특정 스테이지 전체에 시간이나 환경적 규칙으로 영향을 주는 '시스템 기믹'의 공통 인터페이스입니다.
/// </summary>
public interface IStageGimmick
{
    /// <summary>
    /// 해당 스테이지가 시작되거나 기믹이 활성화될 때 최초 1회 호출.
    /// (초기화, 타이머 셋팅 등)
    /// </summary>
    void OnStageStart();

    /// <summary>
    /// StageManager의 Update 등에서 매 프레임 호출.
    /// (타이머 차감, 주기적인 기믹 발동 체크 등)
    /// </summary>
    void OnStageUpdate();

    /// <summary>
    /// 스테이지가 끝나거나 기믹이 종료될 때 1회 호출.
    /// (남은 데이터 정리 등)
    /// </summary>
    void OnStageEnd();
}