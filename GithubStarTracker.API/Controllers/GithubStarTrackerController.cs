using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Net;
using Octokit;

namespace GithubStarTracker.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GithubStarTrackerController : ControllerBase
    {
        private readonly GitHubClient _githubClient;
        public GithubStarTrackerController()
        {
            _githubClient = new GitHubClient(new Octokit.ProductHeaderValue("GitHubStarTracker"));

            _githubClient.Credentials = new Credentials("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");
        }

        [HttpGet("repo_info")]
        public async Task<IActionResult> GetRepoInfo([FromQuery] string repoName)
        {
            if (string.IsNullOrWhiteSpace(repoName))
            {
                return BadRequest("Repository name is required");
            }

            try
            {
                var parts = repoName.Split('/');
                if (parts.Length != 2)
                {
                    return BadRequest("Repository name must be in the format 'owner/repo'");
                }

                var owner = parts[0];
                var repo = parts[1];

                var repository = await _githubClient.Repository.Get(owner, repo);

                var response = new
                {
                    RepoName = repoName,
                    Description = repository.Description,
                    Stars = repository.StargazersCount
                };

                return Ok(response);
            }
            catch (NotFoundException)
            {
                return NotFound($"Repository '{repoName}' not found");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }


        [HttpGet("org_repos")]
        public async Task<IActionResult> GetOrgRepos([FromQuery] string orgName,[FromQuery] int? page = 1,[FromQuery] int? pageSize = 10,
        [FromQuery] string sort = null)
        {
            if (string.IsNullOrWhiteSpace(orgName))
            {
                return BadRequest("Organization name is required");
            }

            try
            {
                var repos = await _githubClient.Repository.GetAllForOrg(orgName);

                // Transform repositories
                var repoQuery = repos.Select(repo => new
                {
                    RepoName = $"{orgName}/{repo.Name}",
                    Description = repo.Description,
                    Stars = repo.StargazersCount
                });

                // Sorting logic
                repoQuery = sort?.ToLower() switch
                {
                    "name_asc" => repoQuery.OrderBy(r => r.RepoName),
                    "name_desc" => repoQuery.OrderByDescending(r => r.RepoName),
                    "stars_asc" => repoQuery.OrderBy(r => r.Stars),
                    "stars_desc" => repoQuery.OrderByDescending(r => r.Stars),
                    "description_asc" => repoQuery.OrderBy(r => r.Description),
                    "description_desc" => repoQuery.OrderByDescending(r => r.Description),
                    _ => repoQuery // Default sorting (no specific sort applied)
                };

                // Pagination
                int currentPage = page ?? 1;
                int currentPageSize = pageSize ?? 10;

                var paginatedResults = repoQuery
                    .Skip((currentPage - 1) * currentPageSize)
                    .Take(currentPageSize)
                    .ToList();

                // Prepare response with pagination metadata
                var response = new
                {
                    TotalRepositories = repos.Count,
                    Page = currentPage,
                    PageSize = currentPageSize,
                    TotalPages = (int)Math.Ceiling((double)repos.Count / currentPageSize),
                    Repositories = paginatedResults
                };

                return Ok(response);
            }
            catch (NotFoundException)
            {
                return NotFound($"Organization '{orgName}' not found");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        [HttpGet("multiple_repos")]
        public async Task<IActionResult> GetMultipleRepos([FromQuery] string[] repoNames)
        {
            if (repoNames == null || repoNames.Length == 0)
            {
                return BadRequest("At least one repository name is required");
            }

            var results = new List<object>();
            var notFoundRepos = new List<string>();

            foreach (var repoName in repoNames)
            {
                try
                {
                    var parts = repoName.Split('/');
                    if (parts.Length != 2)
                    {
                        results.Add(new
                        {
                            RepoName = repoName,
                            Error = "Repository name must be in the format 'owner/repo'"
                        });
                        continue;
                    }

                    var owner = parts[0];
                    var repo = parts[1];

                    var repository = await _githubClient.Repository.Get(owner, repo);

                    results.Add(new
                    {
                        RepoName = repoName,
                        Description = repository.Description,
                        Stars = repository.StargazersCount
                    });
                }
                catch (NotFoundException)
                {
                    notFoundRepos.Add(repoName);
                }
                catch (Exception ex)
                {
                    results.Add(new
                    {
                        RepoName = repoName,
                        Error = ex.Message
                    });
                }
            }

            if (results.Count == 0 && notFoundRepos.Count > 0)
            {
                return NotFound($"Repositories not found: {string.Join(", ", notFoundRepos)}");
            }

            return Ok(new
            {
                Results = results,
                NotFound = notFoundRepos
            });
        }
    }
}
