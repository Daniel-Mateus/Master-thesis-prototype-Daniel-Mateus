using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ModelTransformerWebApp.Data;
using ModelTransformerWebApp.Models;
using NuGet.Packaging;

namespace ModelTransformerWebApp.Controllers
{
    public class PatternsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PatternsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Patterns
        public async Task<IActionResult> Index()
        {
            List<Pattern> patterns = await _context.Pattern.ToListAsync();
            bool gotPatterns = patterns.Any();
            ViewData["gotPatterns"] = gotPatterns;
            ViewBag.gotPatterns = gotPatterns;

            return _context.Pattern != null ?
                        View(await _context.Pattern.ToListAsync()) :
                        Problem("Entity set 'ApplicationDbContext.Pattern'  is null.");
        }

        // GET: Nodes
        public async Task<IActionResult> DisplayNodes()
        {
            return _context.Node != null ?
                        View(await _context.Node.ToListAsync()) :
                        Problem("Entity set 'ApplicationDbContext.Node'  is null.");
        }

        // GET: Patterns/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.Pattern == null)
            {
                return NotFound();
            }

            var pattern = await _context.Pattern
                .FirstOrDefaultAsync(m => m.Id == id);
            if (pattern == null)
            {
                return NotFound();
            }

            return View(pattern);
        }

        // GET: Patterns/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Patterns/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,NodeTypes,IsSameLane,IsSameProcess,USTemplate")] Pattern pattern)
        {

            if (ModelState.IsValid)
            {
                _context.Add(pattern);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(pattern);
        }

        [HttpPost, ActionName("AddDefaultPatterns")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddDefaultPatterns()
        {
            List<Pattern> defaultPatterns = new List<Pattern>();
            Pattern p1 = new Pattern("STARTEVENT;TASK",true,true, "As {LANE} I receive a(n) {STARTEVENT} in order to {TASK}.");
            Pattern p2 = new Pattern("TASK;EXCLUSIVEGATEWAY;TASK", true, true, "As {LANE} I want to {TASK} in order to {TASK}.");
            Pattern p3 = new Pattern("TASK;EXCLUSIVEGATEWAY;TASK", false, true, "As {LANE} I want to {TASK} in order to {LANE} can {TASK}.");
            Pattern p4 = new Pattern("TASK;INTERMEDIATETHROWEVENTMESSAGE", false, true, "As {LANE} I want to {TASK} in order to send a(n) {INTERMEDIATETHROWEVENTMESSAGE} to {PROCESS}.");
            Pattern p5 = new Pattern("INTERMEDIATECATCHEVENTMESSAGE;EXCLUSIVEGATEWAY;TASK", true, true, "As {LANE} I receive a(n) {INTERMEDIATECATCHEVENTMESSAGE} in order to {TASK}.");
            Pattern p6 = new Pattern("INTERMEDIATECATCHEVENTTIMER;TASK", true, true, "As {LANE} I want to wait for {INTERMEDIATECATCHEVENTTIMER} in order to {TASK}.");
            Pattern p7 = new Pattern("TASK;TASK", true, true, "As {LANE} I want to {TASK} in order to {TASK}.");
            Pattern p8 = new Pattern("STARTEVENT;ENDEVENT", true, true, "As {PROCESS} I want to send {STARTEVENT} in order to get {ENDEVENT}.");
            Pattern p9 = new Pattern("TASK;ENDEVENT", true, true, "As {LANE} I want to {TASK} in order to send {ENDEVENT} to {PROCESS}.");
            Pattern p10 = new Pattern("STARTEVENT;EXCLUSIVEGATEWAY;TASK", true, true, "As {LANE} I receive a(n) {STARTEVENT} in order to {TASK}");
            Pattern p11 = new Pattern("TASK;INTERMEDIATECATCHEVENTTIMER;ENDEVENT", true, true, "As {LANE} I want to {TASK}, then I want to wait for {INTERMEDIATECATCHEVENTTIMER} in order" +
                " to send {ENDEVENT} to {PROCESS}.");
            Pattern p12 = new Pattern("INTERMEDIATECATCHEVENTTIMER;EXCLUSIVEGATEWAY;TASK", true, true, "As {LANE} I want to wait for {INTERMEDIATECATCHEVENTTIMER} in order to {TASK}.");
            Pattern p13 = new Pattern("TASK;ENDEVENT", false, true, "As {LANE} I want to {TASK} in order to {LANE} can send {ENDEVENT} to {PROCESS}.");
            Pattern p14 = new Pattern("INTERMEDIATETHROWEVENTMESSAGE;INTERMEDIATECATCHEVENTTIMER;TASK", false, true, "As {LANE} I want to send a(n) {INTERMEDIATETHROWEVENTMESSAGE} " +
    "to {PROCESS}, considering that I have to wait for {INTERMEDIATECATCHEVENTTIMER} in order to {LANE} can {TASK}.");
            Pattern p15 = new Pattern("STARTEVENT;ENDEVENT", false, true, "As {PROCESS} I want to send {STARTEVENT} in order to get {ENDEVENT}.");
            Pattern p16 = new Pattern("TASK;INTERMEDIATETHROWEVENTMESSAGE", true, true, "As {LANE} I want to {TASK} in order to send a(n) {INTERMEDIATETHROWEVENTMESSAGE} to {PROCESS}.");
            Pattern p17 = new Pattern("STARTEVENT;INCLUSIVEGATEWAY;TASK", true, true, "As {LANE} I receive a(n) {STARTEVENT} in order to {TASK}");
            Pattern p18 = new Pattern("BOUNDARYEVENT;TASK;ENDEVENT", true, true, "{LANE} {BOUNDARYEVENT} in {TASK}\nGiven {LANE} starts " +
    "{TASK}\nWhen {BOUNDARYEVENT} in {TASK}\nThen {LANE} sends {ENDEVENT}.", false);
            Pattern p19 = new Pattern("PARALLELGATEWAY", true, true, "As {LANEN}, I want to execute {ACTIVITYN} together.");
            Pattern p22 = new Pattern("INCLUSIVEGATEWAY", true, true, "As {LANEN}, I will follow one or more of these paths: {PATHN} in order to" +
                " execute one or more of the respective activities: {ACTIVITYN}.");
            Pattern p23 = new Pattern("TASK;EXCLUSIVEGATEWAY;ENDEVENT", true, true, "As {LANE} I want to {TASK} in order to send {ENDEVENT} to " +
                "{OUTGOINGFLOWN} depending on {EXCLUSIVEGATEWAYNAME}.");


            //Scenarios Patterns
            Pattern p20 = new Pattern("TASK;EXCLUSIVEGATEWAY;INTERMEDIATETHROWEVENTMESSAGE", true, true, "{LANE} {TASK} leads to " +
                "{GATEWAYPATH}\nGiven {LANE} made {TASK}\n When {TASK} leads to {GATEWAYPATH}\nThen {LANE} sends {INTERMEDIATETHROWEVENTMESSAGE}.",
                false);
            Pattern p21 = new Pattern("INTERMEDIATETHROWEVENTMESSAGE;EVENTBASEDGATEWAY;INTERMEDIATECATCHEVENTTIMER;ENDEVENT", true, true,
                "{LANE} {INTERMEDIATETHROWEVENTMESSAGE} {INTERMEDIATECATCHEVENTTIMER}\nGiven {LANE} send " +
                "{INTERMEDIATETHROWEVENTMESSAGE}\nWhen {INTERMEDIATECATCHEVENTTIMER}\nThen {LANE} sends {ENDEVENT}.", false);

            Pattern p24 = new Pattern("STARTEVENT;EXCLUSIVEGATEWAY;TASK", true, true,
                "{LANE} get {STARTEVENT} leads to {GATEWAYPATH}\nGiven {STARTEVENT} made\n When {STARTEVENT} leads to {GATEWAYPATH}\nThen {LANE} " +
                "{TASK}.", false);
            Pattern p25 = new Pattern("TASK;EXCLUSIVEGATEWAY;ENDEVENT", true, true,
                "{LANE} {TASK}\nGiven {TASK} made\n When {EXCLUSIVEGATEWAYNAME} leads to {GATEWAYPATH}\nThen {LANE} " +
                " sends {ENDEVENT}.", false);
            Pattern p26 = new Pattern("TASK;EXCLUSIVEGATEWAY;TASK", true, true,
                "{LANE} {TASK}\nGiven {TASK} made\n When {EXCLUSIVEGATEWAYNAME} leads to {GATEWAYPATH}\nThen {LANE} " +
                " {TASK}.", false);

            defaultPatterns.AddRange(new List<Pattern>
            { 
                p1,p2,p3,p4,p5,p6,p7,p8,p9,p10,p11,p12,p13,p14,p15,p16,p17,p18,p19,p20,p21,p22,p23,p24,p25,p26
            });

            _context.Pattern.AddRange(defaultPatterns);
            await _context.SaveChangesAsync();
            List<Pattern> patterns = await _context.Pattern.ToListAsync();
            ViewBag.gotPatterns = true;

            return View("Index", patterns);
        }

        // GET: Patterns/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.Pattern == null)
            {
                return NotFound();
            }

            var pattern = await _context.Pattern.FindAsync(id);
            if (pattern == null)
            {
                return NotFound();
            }
            return View(pattern);
        }

        // POST: Patterns/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,NodeTypes,IsSameLane,IsSameProcess,USTemplate")] Pattern pattern)
        {
            if (id != pattern.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(pattern);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PatternExists(pattern.Id))
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
            return View(pattern);
        }

        // GET: Patterns/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.Pattern == null)
            {
                return NotFound();
            }

            var pattern = await _context.Pattern
                .FirstOrDefaultAsync(m => m.Id == id);
            if (pattern == null)
            {
                return NotFound();
            }

            return View(pattern);
        }

        // POST: Patterns/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (_context.Pattern == null)
            {
                return Problem("Entity set 'ApplicationDbContext.Pattern'  is null.");
            }
            var pattern = await _context.Pattern.FindAsync(id);
            if (pattern != null)
            {
                _context.Pattern.Remove(pattern);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool PatternExists(int id)
        {
            return (_context.Pattern?.Any(e => e.Id == id)).GetValueOrDefault();
        }
    }
}
