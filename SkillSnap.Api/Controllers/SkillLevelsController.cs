using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkillSnap.Api.Data;
using SkillSnap.Api.Models;

namespace SkillSnap.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class SkillLevelsController(SkillSnapContext db, SkillLevelCache levels) : ControllerBase
{
    [ProducesResponseType<IReadOnlyList<SkillLevelDto>>(200)]
    [HttpGet(Name = "GetSkillLevels")]
    public async Task<ActionResult<IReadOnlyList<SkillLevelDto>>> GetSkillLevels(CancellationToken ct) =>
        Ok(await levels.GetAsync(db, ct));
}
