﻿using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OA.Core.Constants;
using OA.Core.Models;
using OA.Core.Repositories;
using OA.Core.Services;
using OA.Core.VModels;
using OA.Domain.VModels.Role;
using OA.Infrastructure.EF.Context;
using OA.Infrastructure.EF.Entities;
using OA.Infrastructure.SQL;
using OA.Repository;
using OA.Service.Helpers;
using System.Dynamic;
using static OA.Core.Constants.MsgConstants;

namespace OA.Service
{
    public class AspNetRoleService : GlobalVariables, IAspNetRoleService
    {
        private readonly ApplicationDbContext _dbContext;

        private readonly RoleManager<AspNetRole> _roleManager;
        private readonly UserManager<AspNetUser> _userManager;
        private static string _nameService = StringConstants.ControllerName.AspNetRole;
        private readonly IMapper _mapper;

        private static BaseConnection _dbConnectSQL = BaseConnection.Instance();
        public AspNetRoleService(ApplicationDbContext dbContext, RoleManager<AspNetRole> roleManager,
            UserManager<AspNetUser> userManager, IHttpContextAccessor contextAccessor, IMapper mapper, IBaseRepository<SysFunction> sysFunctionRepo) : base(contextAccessor)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException("context");
            _userManager = userManager;
            _roleManager = roleManager;
            _mapper = mapper;
        }
        public async Task<ResponseResult> GetById(string id)
        {
            var result = new ResponseResult();
            var entity = await _roleManager.FindByIdAsync(id) ?? throw new NotFoundException(WarningMessages.NotFoundData);
            var roleGetByIdVModel = _mapper.Map<AspNetRoleGetByIdVModel>(entity);
            result.Data = roleGetByIdVModel;
            return result;
        }

        public Task<ResponseResult> GetAll(FiltersGetAllByQueryStringRoleVModel model)
        {
            var result = new ResponseResult();

            var query = _roleManager.Roles.OrderBy(x => x.Id).Where(x =>true == x.IsActive). AsQueryable();

            query = query.Where(x => x.Name != null && x.Name.ToLower().Contains(model.Keyword.ToLower()));

            if (!string.IsNullOrEmpty(model.SortBy))
            {
                if (model.IsDescending == false)
                {
                    query = query.OrderBy(x => EF.Property<object>(x, model.SortBy));
                }
                else
                {
                    query = query.OrderByDescending(x => EF.Property<object>(x, model.SortBy));
                }
            }
            else
            {
                query = query.OrderByDescending(x => x.Id);
            }

            var data = new Pagination
            {
                Records = query.Skip((model.PageNumber - 1) * model.PageSize).Take(model.PageSize),
                TotalRecords = query.Count()
            };
            data.Records = data.Records.Select(r => Utilities.ConvertModel<AspNetRoleGetAllVModel>(r));
            result.Data = data;

            return Task.FromResult(result);
        }

        public async Task CheckValidRoleName(string roleName)
        {
            var role = await _roleManager.FindByNameAsync(roleName);
            if (role != null)
            {
                throw new BadRequestException(string.Format(MsgConstants.Error404Messages.ErrorCheckValidRoleName, "RoleName"));
            }
        }

        public async Task<ResponseResult> GetJsonHasFunctionByRoleId(string id)
        {
            var result = new ResponseResult();
            var entity = await _roleManager.FindByIdAsync(id) ?? throw new NotFoundException(WarningMessages.NotFoundData);
            dynamic objResult = new ExpandoObject();
            objResult.RoleId = entity.Id;
            objResult.Name = entity.Name;
            objResult.JsonRoleHasFunctions = entity.JsonRoleHasFunctions;
            result.Data = objResult;

            return result;
        }
        public async Task Create(AspNetRoleCreateVModel model)
        {
            var roleExist = await _roleManager.RoleExistsAsync(model.Name);
            if (!roleExist)
            {
                var entityCreated = _mapper.Map<AspNetRole>(model);
                entityCreated.Id = await SetIdMax(model);
                entityCreated.NormalizedName = _roleManager.NormalizeKey(model.Name);
                entityCreated.CreatedDate = DateTime.Now;
                entityCreated.CreatedBy = GlobalUserName ?? null;
                await _roleManager.CreateAsync(entityCreated);
            }
            else
            {
                throw new ConflictException(string.Format(MsgConstants.Existed.ObjectIsExisted, _nameService));
            }
        }

        public async Task Update(AspNetRoleUpdateVModel model)
        {
            var entity = await _roleManager.FindByIdAsync(model.Id);
            if (entity != null)
            {
                entity.Name = model.Name;
                entity.IsAdmin = model.IsAdmin;
                entity.LevelRole = model.LevelRole;
                entity.UpdatedDate = DateTime.Now;
                entity.UpdatedBy = GlobalUserName;
                entity.NormalizedName = _roleManager.NormalizeKey(entity.Name);
                entity.IsActive = model.IsActive;
                await _roleManager.UpdateAsync(entity);
            }
            else
            {
                throw new NotFoundException(MsgConstants.WarningMessages.NotFoundData);
            }
        }

        public async Task UpadateJsonHasFunctionByRoleId(UpadateJsonHasFunctionByRoleIdVModel model)
        {
            var entity = await _roleManager.FindByIdAsync(model.Id);
            if (entity != null)
            {
                entity.UpdatedDate = DateTime.Now;
                entity.UpdatedBy = GlobalUserName;
                entity.JsonRoleHasFunctions = model.JsonRoleHasFunctions;
                var updateResult = await _roleManager.UpdateAsync(entity);
                if (!updateResult.Succeeded)
                {
                    throw new BadRequestException(string.Format(MsgConstants.ErrorMessages.ErrorUpdate, _nameService));
                }
            }
            else
            {
                throw new NotFoundException(MsgConstants.WarningMessages.NotFoundData);
            }
        }

        public async Task ChangeStatus(string id)
        {
            var items = await _roleManager.FindByIdAsync(id);
            if (items != null)
            {
                items.UpdatedDate = DateTime.Now;
                items.UpdatedBy = GlobalUserName;
                items.IsActive = !items.IsActive;
                await _roleManager.UpdateAsync(items);
            }
            else
            {
                throw new NotFoundException(MsgConstants.WarningMessages.NotFoundData);
            }
        }

        public async Task Remove(string id)
        {
            var result = new ResponseResult();
            var entity = await _roleManager.FindByIdAsync(id);
            if (entity != null)
            {
                var userRoles = await _userManager.GetUsersInRoleAsync(entity.Name ?? string.Empty);
                if (userRoles.Any())
                {
                    foreach (var user in userRoles)
                    {
                        await _userManager.RemoveFromRoleAsync(user, entity.Name ?? string.Empty);
                    }
                }
                var deleteResult = await _roleManager.DeleteAsync(entity);
                if (!deleteResult.Succeeded)
                {
                    throw new BadRequestException(string.Format(MsgConstants.ErrorMessages.ErrorRemove, _nameService));
                }
            }
            else
            {
                throw new NotFoundException(MsgConstants.WarningMessages.NotFoundData);
            }
        }

        public virtual async Task<ExportStream> ExportFile(FiltersGetAllByQueryStringRoleVModel model, ExportFileVModel exportModel)
        {
            model.IsExport = true;
            var result = await GetAll(model);

            var records = _mapper.Map<IEnumerable<AspNetRoleExport>>(result.Data?.Records);
            var exportData = ImportExportHelper<AspNetRoleExport>.ExportFile(exportModel, records);
            return await Task.FromResult(exportData);
        }

        public async Task<string> SetIdMax(AspNetRoleCreateVModel model)
        {
            var entity = _mapper.Map<AspNetRoleCreateVModel, AspNetRole>(model);

            // Lấy danh sách các ID hiện có từ cơ sở dữ liệu
            var idList = await _dbContext.AspNetRoles
                .Select(x => x.Id)
                .ToListAsync();

            // Tìm ID lớn nhất dựa trên phần số trong ID
            var highestId = idList
                .Where(id => id.Length > 1 && id.StartsWith("R")) // Lọc các ID hợp lệ bắt đầu bằng "R"
                .Select(id => new
                {
                    originalId = id,
                    numPart = int.TryParse(id.Substring(1), out int number) ? number : -1 // Phần số sau "R"
                })
                .OrderByDescending(x => x.numPart) // Sắp xếp giảm dần theo phần số
                .FirstOrDefault();

            // Nếu tồn tại ID lớn nhất
            if (highestId != null && highestId.numPart != -1)
            {
                // Tăng số ID lên 1
                var newIdNumber = highestId.numPart + 1;

                // Gán ID mới với định dạng "Rxxxx"
                entity.Id = "R" + newIdNumber.ToString("D4");
            }
            else
            {
                // Nếu không có ID hợp lệ, bắt đầu với "R0001"
                entity.Id = "R0001";
            }

            // Trả về ID mới
            return entity.Id;
        }


    }

}
