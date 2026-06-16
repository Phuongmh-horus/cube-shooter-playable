public interface BaseLevelGenerator
{
    /// <summary>
    /// Gọi khi cần khởi tạo pool trước khi tải level mới.
    /// </summary>
    /// <returns></returns>
    System.Collections.IEnumerator OnInitPoolAsync();

    /// <summary>
    /// Gọi khi cần tải lại dữ liệu level mới.
    /// </summary>
    System.Collections.IEnumerator OnLoadLevel(RoundDataBytes newRoundData);

    /// <summary>
    /// Gọi khi cần xóa toàn bộ dữ liệu hiện tại.
    /// </summary>
    void OnClear();
}