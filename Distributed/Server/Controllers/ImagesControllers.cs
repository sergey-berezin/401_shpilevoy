
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Server.Database;

namespace Server.Controllers
{
    [ApiController]
    [Route("api/arcFace")]
    public class ArcFaceController : ControllerBase
    {
        private IImageDb db;

        public ArcFaceController(IImageDb db)
        {
            this.db = db;
        }

        [HttpPost]
        [Route("images")]
        public async Task<int[]> PostImages([FromBody] List<ImageMinInfo> images, CancellationToken token)
        {
            return await db.PostImages(images, token);
        }


        [HttpGet]
        [Route("images")]
        public async Task<ActionResult<List<ImageInfo>>> GetAllImages()
        {
            var res = await db.GetImages();
            if (res != null)
                return res;
            else
                return StatusCode(404, "Cant return images");
        }

        [HttpDelete]
        [Route("images")]
        public async Task<bool> DeleteAllImages()
        {
            return await db.DeleteAllImages();
        }

        [HttpDelete]
        [Route("images/id")]
        public async Task<ActionResult<bool>> DeleteImage(int id)
        {
            try
            {
                return await db.DeleteImage(id);
            }
            catch
            {
                return StatusCode(404, $"No image with id {id} in databse");
            }
        }

        [HttpGet]
        [Route("compare")]
        public async Task<ActionResult<Metrics>> GetCompare([FromQuery] int id1, int id2)
        {
            try
            {
                var res = await db.GetCompareImages(id1, id2);
                return res;
            }
            catch (Exception ex)
            {
                return StatusCode(400, ex.Message);
            }
        }
    }
}
