import { useState, useEffect } from 'react'
import api from '@/lib/api'
import { Button } from '@/components/ui/button'
import { Loader2, BookOpen, Search, Bookmark, BookmarkCheck, Sun, X } from 'lucide-react'

interface BibleBook {
  id: string
  name: string
  abbreviation: string
  testament: string
  bookOrder: number
  chapterCount: number
}

interface BibleVerse {
  id: string
  bookId: string
  bookName: string
  chapter: number
  verseNumber: number
  text: string
  isBookmarked: boolean
  bookmarkColor?: string
  bookmarkNote?: string
}

interface DailyVerse {
  id: string
  reference: string
  text: string
}

interface BookmarkItem {
  id: string
  verseId: string
  reference: string
  text: string
  note?: string
  color: string
}

const BOOKMARK_COLORS: Record<string, string> = {
  yellow: 'bg-yellow-500/20 border-yellow-500/30',
  blue: 'bg-blue-500/20 border-blue-500/30',
  green: 'bg-green-500/20 border-green-500/30',
  red: 'bg-red-500/20 border-red-500/30',
  purple: 'bg-purple-500/20 border-purple-500/30',
}

export default function BiblePage() {
  const [books, setBooks] = useState<BibleBook[]>([])
  const [selectedBook, setSelectedBook] = useState<string>('')
  const [selectedChapter, setSelectedChapter] = useState(1)
  const [verses, setVerses] = useState<BibleVerse[]>([])
  const [dailyVerse, setDailyVerse] = useState<DailyVerse | null>(null)
  const [bookmarks, setBookmarks] = useState<BookmarkItem[]>([])
  const [searchQuery, setSearchQuery] = useState('')
  const [searchResults, setSearchResults] = useState<Array<{ id: string; reference: string; text: string }>>([])
  const [activeTab, setActiveTab] = useState<'read' | 'daily' | 'bookmarks'>('read')
  const [isLoading, setIsLoading] = useState(true)
  const [isSearching, setIsSearching] = useState(false)
  const [bookmarkNote, setBookmarkNote] = useState('')
  const [showBookmarkForm, setShowBookmarkForm] = useState<string | null>(null)

  useEffect(() => {
    loadBooks()
    loadDailyVerse()
    loadBookmarks()
  }, [])

  useEffect(() => {
    if (selectedBook) {
      loadChapter()
    }
  }, [selectedBook, selectedChapter])

  const loadBooks = async () => {
    try {
      const { data } = await api.get('/bible/books')
      setBooks(data)
      if (data.length > 0) {
        setSelectedBook(data[0].id)
      }
    } catch (err) {
      console.error(err)
    } finally {
      setIsLoading(false)
    }
  }

  const loadChapter = async () => {
    if (!selectedBook) return
    try {
      const { data } = await api.get(`/bible/books/${selectedBook}/chapters/${selectedChapter}`)
      setVerses(data.verses)
    } catch (err) {
      console.error(err)
    }
  }

  const loadDailyVerse = async () => {
    try {
      const { data } = await api.get('/bible/daily')
      setDailyVerse(data)
    } catch (err) {
      // Bible may not be seeded yet
    }
  }

  const loadBookmarks = async () => {
    try {
      const { data } = await api.get('/bible/bookmarks')
      setBookmarks(data)
    } catch (err) {
      console.error(err)
    }
  }

  const search = async () => {
    if (!searchQuery.trim() || searchQuery.length < 2) return
    setIsSearching(true)
    try {
      const { data } = await api.get(`/bible/search?q=${encodeURIComponent(searchQuery)}`)
      setSearchResults(data)
    } catch (err) {
      console.error(err)
    } finally {
      setIsSearching(false)
    }
  }

  const toggleBookmark = async (verseId: string) => {
    const verse = verses.find(v => v.id === verseId)
    if (!verse) return

    if (verse.isBookmarked) {
      const bookmark = bookmarks.find(b => b.verseId === verseId)
      if (bookmark) {
        await api.delete(`/bible/bookmarks/${bookmark.id}`)
        loadBookmarks()
        loadChapter()
      }
    } else {
      setShowBookmarkForm(verseId)
    }
  }

  const saveBookmark = async (verseId: string) => {
    try {
      await api.post('/bible/bookmarks', {
        verseId,
        note: bookmarkNote || undefined,
        color: 'yellow'
      })
      setShowBookmarkForm(null)
      setBookmarkNote('')
      loadBookmarks()
      loadChapter()
    } catch (err) {
      console.error(err)
    }
  }

  const otBooks = books.filter(b => b.testament === 'OT')
  const ntBooks = books.filter(b => b.testament === 'NT')
  const currentBook = books.find(b => b.id === selectedBook)

  if (isLoading) {
    return (
      <div className="p-8 flex items-center justify-center min-h-screen">
        <Loader2 className="h-8 w-8 animate-spin text-primary" />
      </div>
    )
  }

  return (
    <div className="p-8 max-w-4xl mx-auto">
      <div className="flex items-center justify-between mb-6">
        <h1 className="text-2xl font-bold flex items-center gap-2">
          <BookOpen className="h-6 w-6 text-primary" />
          Bible (WEB)
        </h1>
        <div className="flex gap-2">
          <Button variant={activeTab === 'read' ? 'default' : 'outline'} size="sm" onClick={() => setActiveTab('read')}>
            Read
          </Button>
          <Button variant={activeTab === 'daily' ? 'default' : 'outline'} size="sm" onClick={() => setActiveTab('daily')}>
            <Sun className="mr-1 h-4 w-4" />
            Daily
          </Button>
          <Button variant={activeTab === 'bookmarks' ? 'default' : 'outline'} size="sm" onClick={() => setActiveTab('bookmarks')}>
            <Bookmark className="mr-1 h-4 w-4" />
            Bookmarks ({bookmarks.length})
          </Button>
        </div>
      </div>

      {activeTab === 'read' && (
        <div className="space-y-4">
          {/* Search */}
          <div className="flex gap-2">
            <input
              type="text"
              placeholder="Search verses..."
              className="flex-1 px-3 py-2 border rounded-md bg-background text-sm"
              value={searchQuery}
              onChange={e => setSearchQuery(e.target.value)}
              onKeyDown={e => e.key === 'Enter' && search()}
            />
            <Button size="sm" onClick={search} disabled={isSearching}>
              {isSearching ? <Loader2 className="h-4 w-4 animate-spin" /> : <Search className="h-4 w-4" />}
            </Button>
          </div>

          {searchResults.length > 0 && (
            <div className="bg-card border rounded-lg p-4 space-y-2">
              <div className="flex items-center justify-between">
                <p className="text-sm font-medium">Search Results ({searchResults.length})</p>
                <button onClick={() => setSearchResults([])} className="text-xs text-muted-foreground hover:text-foreground">
                  <X className="h-4 w-4" />
                </button>
              </div>
              {searchResults.map(r => (
                <div key={r.id} className="text-sm border-b pb-2 last:border-0">
                  <span className="font-medium text-primary">{r.reference}</span>
                  <p className="text-muted-foreground">{r.text}</p>
                </div>
              ))}
            </div>
          )}

          {/* Book & Chapter Selectors */}
          <div className="flex gap-3">
            <div className="flex-1">
              <label className="text-xs text-muted-foreground mb-1 block">Book</label>
              <select
                className="w-full px-3 py-2 border rounded-md bg-background text-sm"
                value={selectedBook}
                onChange={e => { setSelectedBook(e.target.value); setSelectedChapter(1) }}
              >
                {otBooks.length > 0 && <optgroup label="Old Testament">
                  {otBooks.map(b => <option key={b.id} value={b.id}>{b.name}</option>)}
                </optgroup>}
                {ntBooks.length > 0 && <optgroup label="New Testament">
                  {ntBooks.map(b => <option key={b.id} value={b.id}>{b.name}</option>)}
                </optgroup>}
              </select>
            </div>
            <div>
              <label className="text-xs text-muted-foreground mb-1 block">Chapter</label>
              <select
                className="w-full px-3 py-2 border rounded-md bg-background text-sm"
                value={selectedChapter}
                onChange={e => setSelectedChapter(parseInt(e.target.value))}
              >
                {currentBook && Array.from({ length: currentBook.chapterCount }, (_, i) => (
                  <option key={i + 1} value={i + 1}>Chapter {i + 1}</option>
                ))}
              </select>
            </div>
          </div>

          {/* Verses */}
          <div className="bg-card border rounded-lg p-6">
            <h2 className="text-lg font-semibold mb-4 text-center">
              {currentBook?.name} {selectedChapter}
            </h2>
            <div className="space-y-3">
              {verses.map(verse => (
                <div
                  key={verse.id}
                  className={`flex gap-3 group ${verse.isBookmarked ? BOOKMARK_COLORS[verse.bookmarkColor || 'yellow'] + ' rounded-md p-2 -mx-2' : ''}`}
                >
                  <span className="text-sm font-bold text-primary/60 w-8 text-right shrink-0 mt-0.5">
                    {verse.verseNumber}
                  </span>
                  <p className="text-sm leading-relaxed flex-1">
                    {verse.text}
                  </p>
                  <button
                    onClick={() => toggleBookmark(verse.id)}
                    className="opacity-0 group-hover:opacity-100 transition-opacity shrink-0 mt-0.5"
                  >
                    {verse.isBookmarked ? (
                      <BookmarkCheck className="h-4 w-4 text-primary" />
                    ) : (
                      <Bookmark className="h-4 w-4 text-muted-foreground hover:text-primary" />
                    )}
                  </button>
                </div>
              ))}
            </div>
          </div>

          {/* Bookmark Form */}
          {showBookmarkForm && (
            <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50">
              <div className="bg-card border rounded-lg p-6 w-96">
                <h3 className="font-semibold mb-4">Add Bookmark</h3>
                <textarea
                  className="w-full px-3 py-2 border rounded-md bg-background text-sm mb-4"
                  rows={3}
                  placeholder="Optional note..."
                  value={bookmarkNote}
                  onChange={e => setBookmarkNote(e.target.value)}
                />
                <div className="flex gap-2 justify-end">
                  <Button variant="outline" size="sm" onClick={() => setShowBookmarkForm(null)}>Cancel</Button>
                  <Button size="sm" onClick={() => saveBookmark(showBookmarkForm)}>Save</Button>
                </div>
              </div>
            </div>
          )}
        </div>
      )}

      {activeTab === 'daily' && (
        <div className="bg-card border rounded-lg p-8 text-center">
          <Sun className="h-12 w-12 text-primary mx-auto mb-4" />
          <h2 className="text-lg font-semibold mb-2">Daily Verse</h2>
          {dailyVerse ? (
            <div>
              <p className="text-2xl font-medium italic leading-relaxed mb-4">
                "{dailyVerse.text}"
              </p>
              <p className="text-sm text-muted-foreground">— {dailyVerse.reference}</p>
            </div>
          ) : (
            <p className="text-muted-foreground">No daily verse available. Bible data may not be seeded yet.</p>
          )}
        </div>
      )}

      {activeTab === 'bookmarks' && (
        <div className="space-y-3">
          {bookmarks.length === 0 ? (
            <div className="bg-card border rounded-lg p-8 text-center">
              <Bookmark className="h-12 w-12 text-muted-foreground mx-auto mb-4" />
              <p className="text-muted-foreground">No bookmarks yet.</p>
              <p className="text-sm text-muted-foreground mt-1">Bookmark verses while reading to save them here.</p>
            </div>
          ) : (
            bookmarks.map(b => (
              <div key={b.id} className={`bg-card border rounded-lg p-4 ${BOOKMARK_COLORS[b.color] || ''}`}>
                <div className="flex items-start justify-between">
                  <div>
                    <p className="text-sm font-medium text-primary">{b.reference}</p>
                    <p className="text-sm mt-1">{b.text}</p>
                    {b.note && <p className="text-xs text-muted-foreground mt-1 italic">{b.note}</p>}
                  </div>
                  <Button size="sm" variant="ghost" className="text-destructive shrink-0" onClick={async () => { await api.delete(`/bible/bookmarks/${b.id}`); loadBookmarks(); }}>
                    <X className="h-4 w-4" />
                  </Button>
                </div>
              </div>
            ))
          )}
        </div>
      )}
    </div>
  )
}
