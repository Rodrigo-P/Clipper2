/*******************************************************************************
* Author    :  Angus Johnson                                                   *
* Version   :  10.0 (beta) - aka Clipper2                                      *
* Date      :  6 June 2022                                                     *
* Website   :  http://www.angusj.com                                           *
* Copyright :  Angus Johnson 2010-2022                                         *
* Purpose   :  This is the main polygon clipping module                        *
* License   :  http://www.boost.org/LICENSE_1_0.txt                            *
*******************************************************************************/

#ifndef clipper_engine_h
#define clipper_engine_h

#define CLIPPER2_VERSION "1.0.0"

#include <cstdlib>
#include <queue>
#include <stdexcept>
#include <vector>
#include "clipper.core.h"

namespace Clipper2Lib {

	struct Scanline;
	struct IntersectNode;
	struct Active;
	struct Vertex;
	struct LocalMinima;
	struct OutRec;
	struct Joiner;

	//Note: all clipping operations except for Difference are commutative.
	enum class ClipType { None, Intersection, Union, Difference, Xor };
	
	enum class PointInPolyResult { IsOn, IsInside, IsOutside };

	enum class PathType { Subject, Clip };

	//By far the most widely used filling rules for polygons are EvenOdd
	//and NonZero, sometimes called Alternate and Winding respectively.
	//https://en.wikipedia.org/wiki/Nonzero-rule
	enum class FillRule { EvenOdd, NonZero, Positive, Negative };

	enum class VertexFlags : uint32_t {
		None = 0, OpenStart = 1, OpenEnd = 2, LocalMax = 4, LocalMin = 8
	};

	constexpr enum VertexFlags operator &(enum VertexFlags a, enum VertexFlags b) 
	{
		return (enum VertexFlags)(uint32_t(a) & uint32_t(b));
	}

	constexpr enum VertexFlags operator |(enum VertexFlags a, enum VertexFlags b) 
	{
		return (enum VertexFlags)(uint32_t(a) | uint32_t(b));
	}

	struct Vertex {
		Point64 pt;
		Vertex* next = nullptr;
		Vertex* prev = nullptr;
		VertexFlags flags = VertexFlags::None;
	};

	struct OutPt {
		Point64 pt;
		OutPt*	next = nullptr;
		OutPt*	prev = nullptr;
		OutRec* outrec;
		Joiner* joiner = nullptr;

		OutPt(const Point64& pt_, OutRec* outrec_): pt(pt_), outrec(outrec_) {
			next = this;
			prev = this;
		}
	};

	enum class OutRecState { Undefined = 0, Open = 1, Outer = 2, Inner = 4};

	template <typename T>
	class PolyPath;

	using PolyPath64 = PolyPath<int64_t>;
	using PolyPathD = PolyPath<double>;

	template <typename T>
	using PolyTree = PolyPath<T>;
	using PolyTree64 = PolyTree<int64_t>;
	using PolyTreeD = PolyTree<double>;

	struct OutRec;
	typedef std::vector<OutRec*> OutRecList;

	//OutRec: contains a path in the clipping solution. Edges in the AEL will
	//have OutRec pointers assigned when they form part of the clipping solution.
	struct OutRec {
		size_t idx = 0;
		OutRec* owner = nullptr;
		OutRecList* splits = nullptr;
		Active* front_edge = nullptr;
		Active* back_edge = nullptr;
		OutPt* pts = nullptr;
		PolyPath64* polypath = nullptr;
		OutRecState state = OutRecState::Undefined;
		~OutRec() { if (splits) delete splits; };
	};

	struct Active {
		Point64 bot;
		Point64 top;
		int64_t curr_x = 0;		//current (updated at every new scanline)
		double dx = 0.0;
		int wind_dx = 1;			//1 or -1 depending on winding direction
		int wind_cnt = 0;
		int wind_cnt2 = 0;		//winding count of the opposite polytype
		OutRec* outrec = nullptr;
		//AEL: 'active edge list' (Vatti's AET - active edge table)
		//     a linked list of all edges (from left to right) that are present
		//     (or 'active') within the current scanbeam (a horizontal 'beam' that
		//     sweeps from bottom to top over the paths in the clipping operation).
		Active* prev_in_ael = nullptr;
		Active* next_in_ael = nullptr;
		//SEL: 'sorted edge list' (Vatti's ST - sorted table)
		//     linked list used when sorting edges into their new positions at the
		//     top of scanbeams, but also (re)used to process horizontals.
		Active* prev_in_sel = nullptr;
		Active* next_in_sel = nullptr;
		Active* jump = nullptr;
		Vertex* vertex_top = nullptr;
		LocalMinima* local_min = nullptr;  //the bottom of an edge 'bound' (also Vatti)
		bool is_left_bound = false;
	};

	struct LocalMinima {
		Vertex* vertex;
		PathType polytype;
		bool is_open;
		LocalMinima(Vertex* v, PathType pt, bool open) :
			vertex(v), polytype(pt), is_open(open){}
	};

#ifdef USINGZ
	typedef void (*ZFillCallback)(const Point64& e1bot, const Point64& e1top, 
		const Point64& e2bot, const Point64& e2top, Point64& pt);
#endif

	// ClipperBase -------------------------------------------------------------

	class ClipperBase {
	private:
		ClipType cliptype_ = ClipType::None;
		FillRule fillrule_ = FillRule::EvenOdd;
		int64_t bot_y_ = 0;
		bool error_found_ = false;
		bool has_open_paths_ = false;
		bool minima_list_sorted_ = false;
		bool using_polytree = false;
		Active *actives_ = nullptr;
		Active *sel_ = nullptr;
		Joiner *horz_joiners_ = nullptr;
		std::vector<LocalMinima*> minima_list_;
		std::vector<LocalMinima*>::iterator loc_min_iter_;
		std::vector<Vertex*> vertex_lists_;
		std::priority_queue<int64_t> scanline_list_;
		std::vector<IntersectNode*> intersect_nodes_;
		std::vector<Joiner*> joiner_list_;
		void Reset();
		void InsertScanline(int64_t y);
		bool PopScanline(int64_t &y);
		bool PopLocalMinima(int64_t y, LocalMinima *&local_minima);
		void DisposeAllOutRecs();
		void DisposeVerticesAndLocalMinima();
		void AddLocMin(Vertex &vert, PathType polytype, bool is_open);
		bool IsContributingClosed(const Active &e) const;
		inline bool IsContributingOpen(const Active &e) const;
		void SetWindCountForClosedPathEdge(Active &edge);
		void SetWindCountForOpenPathEdge(Active &e);
		virtual void InsertLocalMinimaIntoAEL(int64_t bot_y);
		void InsertLeftEdge(Active &e);
		inline void PushHorz(Active &e);
		inline bool PopHorz(Active *&e);
		inline OutPt* StartOpenPath(Active &e, const Point64& pt);
		inline void UpdateEdgeIntoAEL(Active *e);
		OutPt* IntersectEdges(Active &e1, Active &e2, const Point64& pt);
		inline void DeleteFromAEL(Active &e);
		inline void AdjustCurrXAndCopyToSEL(const int64_t top_y);
		void DoIntersections(const int64_t top_y);
		void DisposeIntersectNodes();
		void AddNewIntersectNode(Active &e1, Active &e2, const int64_t top_y);
		bool BuildIntersectList(const int64_t top_y);
		void ProcessIntersectList();
		void SwapPositionsInAEL(Active& edge1, Active& edge2);
		OutPt* AddOutPt(const Active &e, const Point64& pt);
		bool TestJoinWithPrev1(const Active& e, int64_t curr_y);
		bool TestJoinWithPrev2(const Active& e, const Point64& curr_pt);
		bool TestJoinWithNext1(const Active& e, int64_t curr_y);
		bool TestJoinWithNext2(const Active& e, const Point64& curr_pt);

		OutPt* AddLocalMinPoly(Active &e1, Active &e2, 
			const Point64& pt, bool is_new = false);
		OutPt* AddLocalMaxPoly(Active &e1, Active &e2, const Point64& pt);
		void DoHorizontal(Active &horz);
		bool ResetHorzDirection(const Active &horz, const Active *max_pair,
			int64_t &horz_left, int64_t &horz_right);
		void DoTopOfScanbeam(const int64_t top_y);
		Active *DoMaxima(Active &e);
		void JoinOutrecPaths(Active &e1, Active &e2);
		bool FixSides(Active& e, Active& e2);
		void CompleteSplit(OutPt* op1, OutPt* op2, OutRec& outrec);
		bool ValidateClosedPathEx(OutRec* outrec);
		void CleanCollinear(OutRec* outrec);
		void FixSelfIntersects(OutRec* outrec);
		OutPt* DoSplitOp(OutPt* outRecOp, OutPt* splitOp);
		Joiner* GetHorzTrialParent(const OutPt* op);
		bool OutPtInTrialHorzList(OutPt* op);
		void SafeDisposeOutPts(OutPt* op);
		void SafeDeleteOutPtJoiners(OutPt* op);
		void AddTrialHorzJoin(OutPt* op);
		void DeleteTrialHorzJoin(OutPt* op);
		void ConvertHorzTrialsToJoins();
		void AddJoin(OutPt* op1, OutPt* op2);
		void DeleteJoin(Joiner* joiner);
		void ProcessJoinerList();
		OutRec* ProcessJoin(Joiner* joiner);
		virtual bool ExecuteInternal(ClipType ct, FillRule ft);
		void BuildPaths(Paths64& solutionClosed, Paths64* solutionOpen);
		void BuildTree(PolyPath64& polytree, Paths64& open_paths);
#ifdef USINGZ
		ZFillCallback zfill_func_; //custom callback 
		void SetZ(const Active& e1, const Active& e2, Point64& pt);
#endif
	protected:
		std::vector<OutRec*> outrec_list_;
		void CleanUp();  //unlike Clear, CleanUp preserves added paths
		void AddPath(const Path64& path, PathType polytype, bool is_open);
		void AddPaths(const Paths64& paths, PathType polytype, bool is_open);

		virtual bool Execute(ClipType clip_type,
			FillRule fill_rule, Paths64& solution_closed);
		virtual bool Execute(ClipType clip_type,
			FillRule fill_rule, Paths64& solution_closed, Paths64& solution_open);
		virtual bool Execute(ClipType clip_type,
			FillRule fill_rule, PolyTree64& polytree, Paths64& open_paths);
	public:
		virtual ~ClipperBase();
		bool PreserveCollinear = true;
		void Clear();
#ifdef USINGZ
		ClipperBase() { zfill_func_ = nullptr; };
		void ZFillFunction(ZFillCallback zFillFunc);
#else
		ClipperBase() {};
#endif
	};

	// PolyPath / PolyTree --------------------------------------------------------

	//PolyTree: is intended as a READ-ONLY data structure for CLOSED paths returned
	//by clipping operations. While this structure is more complex than the
	//alternative Paths structure, it does preserve path 'ownership' - ie those
	//paths that contain (or own) other paths. This will be useful to some users.

	template <typename T>
	class PolyPath {
	private:
		double scale_;
	protected:
		const PolyPath<T>* parent_;
		PolyPath(const PolyPath<T>* parent, 
			const Path<T>& path) : 
			scale_(parent->scale_), parent_(parent), polygon(path) {}
	public:
		Path<T> polygon;
		std::vector<PolyPath*> childs;

		explicit PolyPath(int precision = 0) //NB only for root node
		{  
			scale_ = std::pow(10, precision);
			parent_ = nullptr;
		}

		virtual ~PolyPath() { Clear(); };
		
		//https://en.cppreference.com/w/cpp/language/rule_of_three
		PolyPath(const PolyPath&) = delete;
		PolyPath& operator=(const PolyPath&) = delete;

		void Clear() { 
			for (PolyPath<T>* child : childs) delete child;
			childs.resize(0); 
		}

		void reserve(size_t size)
		{
			if (size > childs.size()) childs.reserve(size);
		}

		PolyPath<T>* AddChild(const Path<T>& path)
		{
			childs.push_back(new PolyPath<T>(this, path));
			return childs.back();
		}

		size_t ChildCount() const { return childs.size(); }

		const PolyPath<T>* operator [] (size_t index) const { return childs[index]; }

		const PolyPath<T>* parent() const { return parent_; }

		bool IsHole() const {
			const PolyPath* pp = parent_;
			bool is_hole = pp;
			while (pp) {
				is_hole = !is_hole;
				pp = pp->parent_;
			}
			return is_hole;
		}

		double Area() const
		{
			double result = Clipper2Lib::Area<T>(polygon);
			for (PolyPath<T> child : childs)
				result += child.Area();
			return result;
		}

	};

	void Polytree64ToPolytreeD(const PolyPath64& polytree, PolyPathD& result);

	class Clipper64 : public ClipperBase
	{
	public:
		void AddSubject(const Paths64& subjects)
		{
			AddPaths(subjects, PathType::Subject, false);
		}
		void AddOpenSubject(const Paths64& open_subjects)
		{
			AddPaths(open_subjects, PathType::Subject, true);
		}
		void AddClip(const Paths64& clips)
		{
			AddPaths(clips, PathType::Clip, false);
		}

		bool Execute(ClipType clip_type,
			FillRule fill_rule, Paths64& closed_paths) override
		{
			return ClipperBase::Execute(clip_type, fill_rule, closed_paths);
		}

		bool Execute(ClipType clip_type,
			FillRule fill_rule, Paths64& closed_paths, Paths64& open_paths) override
		{
			return ClipperBase::Execute(clip_type, fill_rule, closed_paths, open_paths);
		}

		bool Execute(ClipType clip_type,
			FillRule fill_rule, PolyTree64& polytree, Paths64& open_paths) override
		{
			return ClipperBase::Execute(clip_type, fill_rule, polytree, open_paths);
		}

	};

	class ClipperD : public ClipperBase {
	private:
		const double scale_;
	public:
		explicit ClipperD(int precision = 0) : scale_(std::pow(10, precision)) {}

		void AddSubject(const PathsD& subjects)
		{
			AddPaths(ScalePaths<int64_t, double>(subjects, scale_), PathType::Subject, false);
		}

		void AddOpenSubject(const PathsD& open_subjects)
		{
			AddPaths(ScalePaths<int64_t, double>(open_subjects, scale_), PathType::Subject, true);
		}

		void AddClip(const PathsD& clips)
		{
			AddPaths(ScalePaths<int64_t, double>(clips, scale_), PathType::Clip, false);
		}

		bool Execute(ClipType clip_type, FillRule fill_rule, PathsD& closed_paths)
		{
			Paths64 closed_paths64;
			if (!ClipperBase::Execute(clip_type, fill_rule, closed_paths64)) return false;
			closed_paths = ScalePaths<double, int64_t>(closed_paths64, 1 / scale_);
			return true;
		}

		bool Execute(ClipType clip_type,
			FillRule fill_rule, PathsD& closed_paths, PathsD& open_paths)
		{
			Paths64 closed_paths64;
			Paths64 open_paths64;
			if (!ClipperBase::Execute(clip_type,
				fill_rule, closed_paths64, open_paths64)) return false;
			closed_paths = ScalePaths<double, int64_t>(closed_paths64, 1 / scale_);
			open_paths = ScalePaths<double, int64_t>(open_paths64, 1 / scale_);
			return true;
		}

		bool Execute(ClipType clip_type,
			FillRule fill_rule, PolyTreeD& polytree, Paths64& open_paths)
		{
			PolyTree64 tree_result;
			if (!ClipperBase::Execute(clip_type, fill_rule, tree_result, open_paths)) return false;;
			Polytree64ToPolytreeD(tree_result, polytree);
			return true;
		}

	};

	using Clipper = Clipper64;

}  // namespace 

#endif  //clipper_engine_h
