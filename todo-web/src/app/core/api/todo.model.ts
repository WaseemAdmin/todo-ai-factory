export type TodoPriority = 'Low' | 'Normal' | 'High';

export interface Todo {
  id: string;
  title: string;
  description: string | null;
  priority: TodoPriority;
  dueAtUtc: string | null;
  isCompleted: boolean;
  completedAtUtc: string | null;
  isCancelled: boolean;
  cancelledAtUtc: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface TodoWrite {
  title: string;
  description: string | null;
  priority: TodoPriority;
  dueAtUtc: string | null;
}

export interface TodoUpdate extends TodoWrite {
  isCompleted: boolean;
}
