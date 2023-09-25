using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Build.Framework;
using Microsoft.EntityFrameworkCore;
using ModelTransformerWebApp.Data;
using ModelTransformerWebApp.Models;
using Newtonsoft.Json;

namespace ModelTransformerWebApp.Controllers
{
    public class BpmnExtractionsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BpmnExtractionsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: BpmnExtractions
        public async Task<IActionResult> Index()
        {
            List<BpmnExtraction> extractions = await _context.BpmnExtraction.ToListAsync();
            List<Pattern> patterns = await _context.Pattern.ToListAsync();
            if (extractions.Count > 0)
            {
                foreach (var ext in extractions)
                {
                    ext.Nodes = await _context.Node.Where(d => d.BpmnExtractionId == ext.Id).ToListAsync();
                    ext.Patterns.Clear();
                    var patternIDsSplitted = ext.PatternsIDs.Split(";");
                    foreach (var pat in patternIDsSplitted)
                    {
                        if (!String.IsNullOrEmpty(pat))
                        {
                            Pattern patternTemp = patterns.Where(d => d.Id == int.Parse(pat)).FirstOrDefault();
                            ext.Patterns.Add(patternTemp);
                        }
                    }
                }
            }

            return _context.BpmnExtraction != null ?
                          View(extractions) :
                          Problem("Entity set 'ApplicationDbContext.BpmnExtraction'  is null.");
        }

        // GET: BpmnExtractions/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.BpmnExtraction == null)
            {
                return NotFound();
            }

            var bpmnExtraction = await _context.BpmnExtraction
                .FirstOrDefaultAsync(m => m.Id == id);
            if (bpmnExtraction == null)
            {
                return NotFound();
            }

            return View(bpmnExtraction);
        }

        // GET: BpmnExtractions/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: BpmnExtractions/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BpmnExtraction bpmnExtraction)
        {
            Dictionary<string, string> tasks = null;

            if (ModelState.IsValid)
            {
                //Set extraction Date
                bpmnExtraction.ExtractionDate = DateTime.Now;

                //Set File from form
                if (bpmnExtraction.UploadFile != null && bpmnExtraction.UploadFile.Length > 0)
                {
                    var filePath = Path.Combine("./UploadFiles/", bpmnExtraction.UploadFile.FileName);

                    using (var stream = System.IO.File.Create(filePath))
                    {
                        await bpmnExtraction.UploadFile.CopyToAsync(stream);
                    }

                    bpmnExtraction.FilePath = filePath;

                    ExtractSingleton singleton = ExtractSingleton.GetInstance();
                    try
                    {
                        bpmnExtraction.Tasks = singleton.GetTasks(bpmnExtraction);
                    }
                    catch (Exception ex)
                    {
                        throw ex;
                    }

                }
            }
            return FinishCreate(bpmnExtraction, bpmnExtraction.Id);
        }

        // GET: BpmnExtractions/Create
        public IActionResult FinishCreate(BpmnExtraction bpmnExtraction, int id)//BpmnExtraction bpmnExtraction
        {
            ViewBag.ItemListSelectList = new SelectList(bpmnExtraction.Tasks, "Key", "Value");

            return View("FinishCreate", bpmnExtraction);
        }

        // POST: BpmnExtractions/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FinishCreate(BpmnExtraction bpmnExtraction)
        {
            if (ModelState.IsValid)
            {
                //Get patterns List
                List<Pattern> patterns = await _context.Pattern.ToListAsync();
                if (patterns.Count <= 0) return View("../Patterns/Index");


                //Set File from form
                if (bpmnExtraction.FilePath != null && patterns.Count > 0)
                {
                    ExtractSingleton singleton = ExtractSingleton.GetInstance();
                    try
                    {
                        BpmnExtraction bpmnExtractionCompleted = singleton.Extract(bpmnExtraction, patterns, bpmnExtraction.SelectedItems);
                        if (bpmnExtractionCompleted != null) { bpmnExtraction = bpmnExtractionCompleted; }
                    }
                    catch (Exception ex)
                    {
                        throw ex;
                    }

                }
                _context.Add(bpmnExtraction);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(bpmnExtraction);
        }

        [HttpGet]
        public FileStreamResult DownloadFile(int? id, bool isUS)
        {
            var bpmnExtraction = _context.BpmnExtraction
                .FirstOrDefault(m => m.Id == id);

            var string_with_your_data = "";
            string[]? userStoriesSplit = null;

            if (isUS) userStoriesSplit = bpmnExtraction.UserStoriesResult.Split(";");
            else userStoriesSplit = bpmnExtraction.ScenarioResult.Split(";");
            foreach (var us in userStoriesSplit)
            {
                string_with_your_data += us + "\n";
            }

            var byteArray = Encoding.ASCII.GetBytes(string_with_your_data);
            var stream = new MemoryStream(byteArray);
            var fileName = Path.GetFileNameWithoutExtension(bpmnExtraction.FilePath);
            if (isUS) fileName += "_userStories.txt";
            else fileName += "_scenarios.txt";

            return File(stream, "text/plain", fileName);
        }

        // GET: BpmnExtractions/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.BpmnExtraction == null)
            {
                return NotFound();
            }

            var bpmnExtraction = await _context.BpmnExtraction.FindAsync(id);
            if (bpmnExtraction == null)
            {
                return NotFound();
            }
            return View(bpmnExtraction);
        }

        // POST: BpmnExtractions/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,ExtractionDate,UserStoriesResult,ScenarioResult,FilePath")] BpmnExtraction bpmnExtraction)
        {
            if (id != bpmnExtraction.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(bpmnExtraction);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BpmnExtractionExists(bpmnExtraction.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(bpmnExtraction);
        }

        // GET: BpmnExtractions/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.BpmnExtraction == null)
            {
                return NotFound();
            }

            var bpmnExtraction = await _context.BpmnExtraction
                .FirstOrDefaultAsync(m => m.Id == id);
            if (bpmnExtraction == null)
            {
                return NotFound();
            }

            return View(bpmnExtraction);
        }

        // POST: BpmnExtractions/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (_context.BpmnExtraction == null)
            {
                return Problem("Entity set 'ApplicationDbContext.BpmnExtraction'  is null.");
            }
            var bpmnExtraction = await _context.BpmnExtraction.FindAsync(id);
            if (bpmnExtraction != null)
            {
                _context.BpmnExtraction.Remove(bpmnExtraction);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool BpmnExtractionExists(int id)
        {
            return (_context.BpmnExtraction?.Any(e => e.Id == id)).GetValueOrDefault();
        }
    }
}
