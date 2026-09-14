import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import apiClient from '../services/api'
import { useStore } from '../hooks/useStore'
import type { ShoppingList } from '../types'

export default function ShoppingListsPage() {
  const qc = useQueryClient()
  const addLine = useStore((s) => s.addLine)
  const [newListName, setNewListName] = useState('')
  const [creating, setCreating] = useState(false)

  const { data: lists, isLoading } = useQuery({
    queryKey: ['shopping-lists'],
    queryFn: async () => {
      const { data } = await apiClient.get<ShoppingList[]>('/shopping-lists')
      return data
    }
  })

  const createMutation = useMutation({
    mutationFn: async (name: string) => {
      await apiClient.post('/shopping-lists', { name })
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['shopping-lists'] })
      setNewListName('')
      setCreating(false)
    }
  })

  const deleteMutation = useMutation({
    mutationFn: async (listId: string) => {
      await apiClient.delete(`/shopping-lists/${listId}`)
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ['shopping-lists'] })
  })

  async function addListToCart(list: ShoppingList) {
    for (const line of list.lines) {
      addLine({
        articleId: line.articleId,
        articleCode: line.articleCode,
        articleName: line.articleName,
        quantity: line.quantity,
        unitPrice: 0, // Price resolved at checkout
        lineTotal: 0,
        unitOfMeasure: 'PCS'
      })
    }
  }

  return (
    <div>
      <div className="flex items-center justify-between mb-6">
        <h1 className="text-2xl font-bold text-gray-900">Shopping lists</h1>
        <button
          onClick={() => setCreating(true)}
          className="bg-primary-600 text-white px-4 py-2 rounded text-sm hover:bg-primary-700"
        >
          New list
        </button>
      </div>

      {creating && (
        <div className="bg-white rounded-lg shadow-sm p-4 mb-4 flex gap-3">
          <input
            value={newListName}
            onChange={(e) => setNewListName(e.target.value)}
            placeholder="List name"
            className="flex-1 border border-gray-300 rounded px-3 py-2 text-sm"
            autoFocus
          />
          <button
            onClick={() => createMutation.mutate(newListName)}
            disabled={!newListName.trim()}
            className="bg-primary-600 text-white px-4 py-2 rounded text-sm disabled:opacity-40"
          >
            Create
          </button>
          <button onClick={() => setCreating(false)} className="border border-gray-300 text-gray-600 px-4 py-2 rounded text-sm">
            Cancel
          </button>
        </div>
      )}

      {isLoading ? (
        <div className="py-20 text-center text-gray-400">Loading...</div>
      ) : (
        <div className="space-y-4">
          {lists?.map((list) => (
            <div key={list.id} className="bg-white rounded-lg shadow-sm p-4">
              <div className="flex items-center justify-between mb-3">
                <div>
                  <div className="font-medium text-gray-800">{list.name}</div>
                  <div className="text-xs text-gray-400">{list.lines.length} item{list.lines.length !== 1 ? 's' : ''}</div>
                </div>
                <div className="flex gap-2">
                  <button
                    onClick={() => addListToCart(list)}
                    className="bg-primary-600 text-white text-xs px-3 py-1.5 rounded hover:bg-primary-700"
                  >
                    Add all to cart
                  </button>
                  <button
                    onClick={() => deleteMutation.mutate(list.id)}
                    className="text-xs text-red-500 hover:underline"
                  >
                    Delete
                  </button>
                </div>
              </div>
              {list.lines.length > 0 && (
                <div className="text-sm text-gray-500 space-y-1">
                  {list.lines.slice(0, 3).map((line) => (
                    <div key={line.id}>{line.articleName} × {line.quantity}</div>
                  ))}
                  {list.lines.length > 3 && <div className="text-xs text-gray-400">+{list.lines.length - 3} more</div>}
                </div>
              )}
            </div>
          ))}
          {lists?.length === 0 && (
            <div className="py-20 text-center text-gray-400">No shopping lists yet</div>
          )}
        </div>
      )}
    </div>
  )
}
